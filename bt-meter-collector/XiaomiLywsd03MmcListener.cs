using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.InteropServices;

public sealed record Sample(string Mac, short Rssi, double Temperature, double Humidity, ushort BattMv, byte BattPct);

internal sealed class XiaomiLywsd03MmcRawListener
{
    private readonly Func<string, CancellationToken, Task> _deviceAppeared;
    private readonly Func<Sample, CancellationToken, Task> _sampleArrived;
    private readonly ConcurrentDictionary<string, bool> _registeredMacs = new();

    // Native constants for Linux HCI sockets
    private const int AF_BLUETOOTH = 31;
    private const int SOCK_RAW = 3;
    private const int BTPROTO_HCI = 1;
    private const int HCI_CHANNEL_RAW = 0;
    
    [StructLayout(LayoutKind.Sequential)]
    public struct HciFilter
    {
        public uint TypeMask;
        public uint EventMaskL;
        public uint EventMaskH;
        public ushort Opcode;
    }

    const int SOL_HCI = 0;
    const int HCI_FILTER = 2;
    const int HCI_EVENT_PKT = 0x04;
    const int EVT_LE_META_EVENT = 0x3e;

    public XiaomiLywsd03MmcRawListener(Func<string, CancellationToken, Task> deviceAppeared, Func<Sample, CancellationToken, Task> sampleArrived)
    {
        _deviceAppeared = deviceAppeared;
        _sampleArrived = sampleArrived;
    }

    public void StartListening(CancellationToken ct)
    {
        Task.Run(() => RunSocketLoop(ct), ct);
    }

    private async Task RunSocketLoop(CancellationToken ct)
    {
        int fd = socket(AF_BLUETOOTH, SOCK_RAW, BTPROTO_HCI);
        if (fd < 0) throw new Exception("Could not open raw Bluetooth socket. Ensure you have root/CAP_NET_RAW.");

        try
        {
            var addr = new SockAddrHci { family = AF_BLUETOOTH, device = 0, channel = HCI_CHANNEL_RAW }; // hci0
            if (bind(fd, ref addr, Marshal.SizeOf(addr)) < 0) throw new Exception("Failed to bind to hci0.");
            
            var filter = new HciFilter();
            // Set bit for HCI_EVENT_PKT in TypeMask
                        filter.TypeMask = (1U << HCI_EVENT_PKT);
            // Set all bits in EventMask to receive all events, 
            // or specifically set the bit for EVT_LE_META_EVENT.
                        filter.EventMaskL = 0xFFFFFFFF;
                        filter.EventMaskH = 0xFFFFFFFF;

            if (setsockopt(fd, SOL_HCI, HCI_FILTER, ref filter, Marshal.SizeOf(filter)) < 0)
                throw new Exception("Failed to set HCI filter.");

            // Basic BLE Scan Enable Command (Equivalent to 'hcitool lescan --duplicates')
            EnableScanning(fd);

            byte[] buffer = new byte[1024];
            while (!ct.IsCancellationRequested)
            {
                int bytesRead = read(fd, buffer, buffer.Length);
                if (bytesRead > 0)
                {
                    string hex = string.Join(" ", buffer.Take(bytesRead).Select(b => $"0x{b:X2}"));
                    Console.WriteLine($"Received {bytesRead} bytes: {hex}");
                    await ProcessRawHciPacket(buffer.AsMemory(0, bytesRead), ct);
                }
            }
        }
        finally
        {
            close(fd);
        }
    }

    private async Task ProcessRawHciPacket(ReadOnlyMemory<byte> packet, CancellationToken ct)
    {
        // 1. Check header without storing the span as a long-lived local
        if (packet.Length < 15 || 
            packet.Span[0] != 0x04 || 
            packet.Span[1] != 0x3E || 
            packet.Span[3] != 0x02) return;

        // 2. Extract MAC and RSSI (use packet.Span just for the extraction)
        string mac;
        short rssi;
        {
            var span = packet.Span;
            mac = string.Join(":", span.Slice(7, 6).ToArray().Reverse().Select(b => b.ToString("X2")));
            rssi = (short)span[span.Length - 1];
        }

        // 3. Use packet (ReadOnlyMemory) for the loop to allow 'await' inside
        int offset = 14; 
        while (offset + 1 < packet.Length - 1)
        {
            byte len = packet.Span[offset]; // Access span briefly per iteration
            if (len == 0 || offset + len >= packet.Length) break;

            byte type = packet.Span[offset + 1];
            if (type == 0x16 && packet.Span[offset + 2] == 0x1A && packet.Span[offset + 3] == 0x18)
            {
                var payload = packet.Slice(offset + 4, len - 3).ToArray();
                await ParseAndEmit(mac, rssi, payload, ct); // Now this works!
            }
            offset += len + 1;
        }
    }


    private async Task ParseAndEmit(string mac, short rssi, byte[] d, CancellationToken ct)
    {
        if (_registeredMacs.TryAdd(mac, true))
            await _deviceAppeared(mac, ct);

        var sample = new Sample(
            mac, rssi,
            BitConverter.ToInt16(d, 6) / 100.0,
            BitConverter.ToUInt16(d, 8) / 100.0,
            BitConverter.ToUInt16(d, 10),
            d[12]
        );

        await _sampleArrived(sample, ct);
    }

    private void EnableScanning(int fd)
    {
        // For production, you'd send HCI commands to hci0 to ensure it's in scanning mode.
        // Simplest way is to ensure 'sudo hcitool lescan --duplicates' is running or use 'hciconfig'
    }

    #region P/Invoke
    [StructLayout(LayoutKind.Sequential)]
    struct SockAddrHci { public ushort family; public ushort device; public ushort channel; }

    [DllImport("libc", SetLastError = true)] static extern int socket(int domain, int type, int protocol);
    [DllImport("libc", SetLastError = true)] static extern int bind(int sockfd, ref SockAddrHci addr, int addrlen);
    [DllImport("libc", SetLastError = true)] static extern int read(int fd, byte[] buf, int count);
    [DllImport("libc", SetLastError = true)] static extern int close(int fd);
    
    [DllImport("libc", SetLastError = true)]
    public static extern int setsockopt(
        int sockfd, 
        int level, 
        int optname, 
        ref HciFilter optval, 
        int optlen
    );
    #endregion
}
