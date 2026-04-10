using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

public sealed record Sample(string Mac, short Rssi, double Temperature, double Humidity, ushort BattMv, byte BattPct);

public sealed class XiaomiLywsd03MmcRawListener
{
    private readonly Func<string, CancellationToken, Task> _deviceAppeared;
    private readonly Func<Sample, CancellationToken, Task> _sampleArrived;
    private readonly ConcurrentDictionary<string, bool> _registeredMacs = new();

    private const int AF_BLUETOOTH = 31, SOCK_RAW = 3, BTPROTO_HCI = 1, HCI_CHANNEL_RAW = 0;

    public XiaomiLywsd03MmcRawListener(Func<string, CancellationToken, Task> deviceAppeared, Func<Sample, CancellationToken, Task> sampleArrived)
    {
        _deviceAppeared = deviceAppeared;
        _sampleArrived = sampleArrived;
    }

    public async Task StartListeningAsync(CancellationToken ct)
    {
        // 1. Fix I/O Error: Reset the adapter state
        ResetAdapter();

        int fd = socket(AF_BLUETOOTH, SOCK_RAW, BTPROTO_HCI);
        if (fd < 0) throw new Exception("Socket failed. Run with sudo.");

        try
        {
            var addr = new SockAddrHci { family = AF_BLUETOOTH, device = 0, channel = HCI_CHANNEL_RAW };
            if (bind(fd, ref addr, Marshal.SizeOf(addr)) < 0) throw new Exception("Bind failed.");

            // 2. Continuous Updates: Start scan with duplicates enabled
            EnableScanning(fd);

            byte[] buffer = new byte[1024];
            while (!ct.IsCancellationRequested)
            {
                int bytesRead = read(fd, buffer, buffer.Length);
                if (bytesRead > 15) 
                {
                    Console.WriteLine($"Read {bytesRead} bytes from {fd}.");
                    // Pass as ReadOnlyMemory to satisfy the async state machine
                    await ProcessRawHciPacket(buffer.AsMemory(0, bytesRead), ct);
                }
            }
        }
        finally { close(fd); }
    }

    private void ResetAdapter()
    {
        Process.Start("hciconfig", "hci0 down")?.WaitForExit();
        Process.Start("hciconfig", "hci0 up")?.WaitForExit();
    }

    private void EnableScanning(int fd)
    {
        byte[] setParams = { 0x01, 0x0B, 0x20, 0x07, 0x00, 0x10, 0x00, 0x10, 0x00, 0x00, 0x00 };
        write(fd, setParams, setParams.Length);

        // 0x00 at the end = report duplicates (prevents the "silence" issue)
        byte[] setEnable = { 0x01, 0x0C, 0x20, 0x02, 0x01, 0x00 };
        write(fd, setEnable, setEnable.Length);
    }

    private async Task ProcessRawHciPacket(ReadOnlyMemory<byte> packet, CancellationToken ct)
    {
        // Use the Span locally and extract what we need to heap variables
        string mac;
        short rssi;
        byte[]? payload = null;

        {
            var data = packet.Span;
            // Validate HCI Event (0x04=Event, 0x3E=LE Meta, 0x02=Ad Report)
            if (data.Length < 15 || data[0] != 0x04 || data[1] != 0x3E || data[2] != 0x02) return;

            mac = string.Join(":", data.Slice(7, 6).ToArray().Reverse().Select(b => b.ToString("X2")));
            rssi = (short)(sbyte)data[data.Length - 1];

            // Search for PVVX Service Data (0x16 + 0x1A 0x18)
            int offset = 14; 
            while (offset + 4 < data.Length)
            {
                byte len = data[offset];
                if (len == 0 || offset + len >= data.Length) break;

                if (data[offset + 1] == 0x16 && data[offset + 2] == 0x1A && data[offset + 3] == 0x18)
                {
                    // Copy payload to heap array so it survives the 'await' boundary
                    payload = data.Slice(offset + 4, Math.Min(len - 3, data.Length - (offset + 4))).ToArray();
                    break;
                }
                offset += len + 1;
            }
        }

        // Now we can safely await because we aren't holding any Span references
        if (payload != null && payload.Length >= 11)
        {
            if (_registeredMacs.TryAdd(mac, true)) await _deviceAppeared(mac, ct);

            var sample = new Sample(
                mac, rssi,
                BitConverter.ToInt16(payload, 6) / 100.0,
                BitConverter.ToUInt16(payload, 8) / 100.0,
                BitConverter.ToUInt16(payload, 10),
                payload[12]
            );
            await _sampleArrived(sample, ct);
        }
    }

    #region Native
    [StructLayout(LayoutKind.Sequential)]
    struct SockAddrHci { public ushort family, device, channel; }
    [DllImport("libc")] static extern int socket(int d, int t, int p);
    [DllImport("libc")] static extern int bind(int s, ref SockAddrHci a, int l);
    [DllImport("libc")] static extern int read(int f, byte[] b, int c);
    [DllImport("libc")] static extern int write(int f, byte[] b, int c);
    [DllImport("libc")] static extern int close(int f);
    #endregion
}
