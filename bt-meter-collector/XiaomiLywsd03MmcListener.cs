using System.Collections.Concurrent;
using Tmds.DBus;

[DBusInterface("org.bluez.Adapter1")]
public interface IAdapter1 : IDBusObject
{
    Task StartDiscoveryAsync();
    Task SetDiscoveryFilterAsync(IDictionary<string, object> properties);
}

[DBusInterface("org.freedesktop.DBus.ObjectManager")]
public interface IObjectManager : IDBusObject
{
    Task<IDictionary<ObjectPath, IDictionary<string, IDictionary<string, object>>>> GetManagedObjectsAsync();
    Task<IDisposable> WatchInterfacesAddedAsync(
        Action<(ObjectPath path, IDictionary<string, IDictionary<string, object>> interfaces)> handler);
}

[DBusInterface("org.freedesktop.DBus.Properties")]
public interface IProperties : IDBusObject
{
    Task<IDictionary<string, object>> GetAllAsync(string iface);
    
    Task<IDisposable> WatchPropertiesChangedAsync(
        Action<(string iface, IDictionary<string, object> changed, string[] invalidated)> handler);
}

public sealed record Sample(string Mac, short Rssi, double Temperature, double Humidity, ushort BattMv, byte BattPct);

internal sealed class XiaomiLywsd03MmcListener
{
    private readonly Func<string, CancellationToken, Task> _deviceAppeared;
    private readonly Func<Sample, CancellationToken, Task> _sampleArrived;
    private readonly ConcurrentDictionary<ObjectPath, IDisposable> _deviceSubs = new();
    private readonly ConcurrentDictionary<ObjectPath, short> _lastRssiByPath = new();
    private readonly ConcurrentDictionary<string, bool> _registeredMacs = new();

    public XiaomiLywsd03MmcListener(Func<string, CancellationToken, Task> deviceAppeared, Func<Sample, CancellationToken, Task> sampleArrived)
    {
        _deviceAppeared = deviceAppeared;
        _sampleArrived = sampleArrived;
    }

    public void StartListening(CancellationToken cancellationToken)
    {
        var bus = Connection.System;
        var manager = bus.CreateProxy<IObjectManager>("org.bluez", "/");
        var adapter = bus.CreateProxy<IAdapter1>("org.bluez", "/org/bluez/hci0");
        
        var objs = manager.GetManagedObjectsAsync().Result;
        foreach (var (path, ifaces) in objs)
            if (ifaces.ContainsKey("org.bluez.Device1"))
            {
                AttachDeviceAsync(bus, path, cancellationToken).Wait(cancellationToken);
            }
        
        manager.WatchInterfacesAddedAsync(sig =>
        {
            if (sig.interfaces.ContainsKey("org.bluez.Device1"))
                _ = AttachDeviceAsync(bus, sig.path, cancellationToken);
        }).Wait(cancellationToken);
        
        adapter.SetDiscoveryFilterAsync(new Dictionary<string, object>
        {
            ["Transport"] = "le",
            ["DuplicateData"] = true
        }).Wait(cancellationToken);

        adapter.StartDiscoveryAsync().Wait(cancellationToken);
    }
    
    private async Task AttachDeviceAsync(Connection bus, ObjectPath path, CancellationToken cancellationToken)
    {
        if (_deviceSubs.ContainsKey(path)) return;

        var props = bus.CreateProxy<IProperties>("org.bluez", path);
        
        var current = await props.GetAllAsync("org.bluez.Device1");
        Console.WriteLine($"Initial props for {path}: {string.Join(", ", current.Keys)}");
        
        const string PvvxUuid = "0000181a-0000-1000-8000-00805f9b34fb";
        var sub = await props.WatchPropertiesChangedAsync(async change =>
        {
            if (change.iface != "org.bluez.Device1") return;
            
            if (change.changed.TryGetValue("RSSI", out var rssiObj))
            {
                var rssi = (short)rssiObj;
                _lastRssiByPath[path] = rssi;
            }
            
            if (change.changed.TryGetValue("ServiceData", out var value))
            {
                var sd = (IDictionary<string, object>)value;

                foreach (var kv in sd)
                {
                    var uuid = kv.Key.ToLowerInvariant();
                    if (uuid != PvvxUuid)
                        continue;

                    var bytes = (byte[])kv.Value;
                    if (bytes.Length != 15)
                        continue;

                    await ParsePvvx(path, bytes, cancellationToken);
                }
            }
        });

        _deviceSubs[path] = sub;
    }
    
    async Task ParsePvvx(ObjectPath path, byte[] d, CancellationToken cancellationToken)
    {
        if (d.Length < 15) return;

        var mac = string.Join(":",
            new List<byte>{d[5], d[4], d[3], d[2], d[1], d[0]}
                .Select(b => b.ToString("X2")));

        short tempRaw = BitConverter.ToInt16(d, 6);
        ushort humRaw = BitConverter.ToUInt16(d, 8);
        ushort battMv = BitConverter.ToUInt16(d, 10);
        byte battPct = d[12];

        double temp = tempRaw / 100.0;
        double hum = humRaw / 100.0;

        if (_lastRssiByPath.TryGetValue(path, out var rssi))
        {
            if (!_registeredMacs.ContainsKey(mac))
            {
                await _deviceAppeared(mac, cancellationToken);
                _registeredMacs[mac] = true;
            }
            
            var sample = new Sample(mac, rssi, temp, hum, battMv, battPct);
            await _sampleArrived(sample, cancellationToken);
        }
    }
}