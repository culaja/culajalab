using System.Collections.Concurrent;
using Tmds.DBus;

// --- D-Bus Interfaces ---

[DBusInterface("org.bluez.Adapter1")]
public interface IAdapter1 : IDBusObject
{
    Task StartDiscoveryAsync();
    Task SetDiscoveryFilterAsync(IDictionary<string, object> filter);
}

[DBusInterface("org.freedesktop.DBus.ObjectManager")]
public interface IObjectManager : IDBusObject
{
    Task<IDictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>>> GetManagedObjectsAsync();
    Task<IDisposable> WatchInterfacesAddedAsync(Action<(ObjectPath path, IDictionary<string, IDictionary<string, object>> interfaces)> handler);
    Task<IDisposable> WatchInterfacesRemovedAsync(Action<(ObjectPath path, string[] interfaces)> handler);
}

[DBusInterface("org.freedesktop.DBus.Properties")]
public interface IProperties : IDBusObject
{
    Task<IDisposable> WatchPropertiesChangedAsync(Action<(string iface, IDictionary<string, object> changed, string[] invalidated)> handler);
}

// --- Main Listener Logic ---

public sealed record Sample(string Mac, short Rssi, double Temperature, double Humidity, ushort BattMv, byte BattPct);

internal sealed class XiaomiLywsd03MmcListener
{
    private readonly Func<string, CancellationToken, Task> _deviceAppeared;
    private readonly Func<Sample, CancellationToken, Task> _sampleArrived;
    
    // Tracks active property subscriptions per device path
    private readonly ConcurrentDictionary<ObjectPath, IDisposable> _deviceSubs = new();
    private readonly ConcurrentDictionary<ObjectPath, short> _lastRssiByPath = new();
    private readonly ConcurrentDictionary<string, bool> _registeredMacs = new();

    public XiaomiLywsd03MmcListener(Func<string, CancellationToken, Task> deviceAppeared, Func<Sample, CancellationToken, Task> sampleArrived)
    {
        _deviceAppeared = deviceAppeared;
        _sampleArrived = sampleArrived;
    }

    public async Task StartListeningAsync(CancellationToken ct)
    {
        var bus = Connection.System;
        var manager = bus.CreateProxy<IObjectManager>("org.bluez", "/");
        var adapter = bus.CreateProxy<IAdapter1>("org.bluez", "/org/bluez/hci0");

        // 1. Setup Filters: DuplicateData is key for receiving continuous adverts
        await adapter.SetDiscoveryFilterAsync(new Dictionary<string, object> {
            { "Transport", "le" },
            { "DuplicateData", true } 
        });

        // 2. Watch for interface removal: If BlueZ clears its cache, we must be ready to re-attach
        await manager.WatchInterfacesRemovedAsync(sig => {
            if (sig.interfaces.Contains("org.bluez.Device1"))
            {
                if (_deviceSubs.TryRemove(sig.path, out var sub)) {
                    sub.Dispose();
                    _lastRssiByPath.TryRemove(sig.path, out _);
                }
            }
        });

        // 3. Watch for new devices
        await manager.WatchInterfacesAddedAsync(sig => {
            if (sig.interfaces.ContainsKey("org.bluez.Device1"))
                _ = AttachDeviceAsync(bus, sig.path, ct);
        });

        // 4. Attach existing devices found in BlueZ cache
        var objs = await manager.GetManagedObjectsAsync();
        foreach (var (path, ifaces) in objs)
            if (ifaces.ContainsKey("org.bluez.Device1"))
                await AttachDeviceAsync(bus, path, ct);

        // 5. Start Discovery and run a watchdog to keep it active
        await adapter.StartDiscoveryAsync();
        _ = RunDiscoveryWatchdog(adapter, ct);
    }

    private async Task RunDiscoveryWatchdog(IAdapter1 adapter, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));
        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
        {
            try { await adapter.StartDiscoveryAsync(); } 
            catch { /* Ignore 'Operation already in progress' */ }
        }
    }
    
    private async Task AttachDeviceAsync(Connection bus, ObjectPath path, CancellationToken ct)
    {
        // Don't subscribe twice to the same path
        if (_deviceSubs.ContainsKey(path)) return;

        var props = bus.CreateProxy<IProperties>("org.bluez", path);
        const string PvvxUuid = "0000181a-0000-1000-8000-00805f9b34fb";

        var sub = await props.WatchPropertiesChangedAsync(async change =>
        {
            if (change.iface != "org.bluez.Device1") return;
            
            if (change.changed.TryGetValue("RSSI", out var rssiObj))
                _lastRssiByPath[path] = (short)rssiObj;
            
            if (change.changed.TryGetValue("ServiceData", out var value))
            {
                var sd = (IDictionary<string, object>)value;
                foreach (var kv in sd)
                {
                    if (kv.Key.ToLowerInvariant() == PvvxUuid && kv.Value is byte[] bytes && bytes.Length == 15)
                        await ParsePvvx(path, bytes, ct);
                }
            }
        });

        if (!_deviceSubs.TryAdd(path, sub)) sub.Dispose();
    }
    
    private async Task ParsePvvx(ObjectPath path, byte[] d, CancellationToken ct)
    {
        var mac = string.Join(":", d.Take(6).Reverse().Select(b => b.ToString("X2")));

        if (_lastRssiByPath.TryGetValue(path, out var rssi))
        {
            if (_registeredMacs.TryAdd(mac, true))
                await _deviceAppeared(mac, ct);
            
            var sample = new Sample(
                mac, rssi, 
                BitConverter.ToInt16(d, 6) / 100.0, 
                BitConverter.ToUInt16(d, 8) / 100.0, 
                BitConverter.ToUInt16(d, 10), 
                d[12]);

            await _sampleArrived(sample, ct);
        }
    }
}
