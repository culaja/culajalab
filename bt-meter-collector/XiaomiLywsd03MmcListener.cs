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

            byte[] buffer = new byte[1024];
            while (!ct.IsCancellationRequested)
            {
                int bytesRead = read(fd, buffer, buffer.Length);
                if (bytesRead > 0)
                {
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
        var span = packet.Span;

        // Minimum: [packetType][eventCode][paramLen][subevent...]
        if (span.Length < 4)
            return;

        const byte HciEventPkt = 0x04;
        const byte EvtLeMetaEvent = 0x3E;
        const byte EvtLeAdvertisingReport = 0x02;
        const byte EvtLeExtendedAdvertisingReport = 0x0D;
        const ushort EnvironmentalSensingUuid16 = 0x181A;

        if (span[0] != HciEventPkt)
            return;

        if (span[1] != EvtLeMetaEvent)
            return;

        byte subEvent = span[3];

        if (subEvent == EvtLeAdvertisingReport)
        {
            //await ParseLegacyAdvertisingReports(span);
            return;
        }

        if (subEvent == EvtLeExtendedAdvertisingReport)
        {
            await ParseExtendedAdvertisingReports(span);
        }

        return;

        Task ParseLegacyAdvertisingReports(ReadOnlySpan<byte> s)
        {
            // Format:
            // [0]=0x04 [1]=0x3E [2]=paramLen [3]=0x02 [4]=numReports ...
            if (s.Length < 5)
                return Task.CompletedTask;

            int offset = 4;
            int numReports = s[offset++];
            for (int i = 0; i < numReports; i++)
            {
                // Legacy LE Advertising Report layout:
                // evt_type(1), addr_type(1), addr(6), data_len(1), data(N), rssi(1)
                if (offset + 10 > s.Length)
                    return Task.CompletedTask;

                byte evtType = s[offset++];
                byte addrType = s[offset++];
                _ = evtType;
                _ = addrType;

                string reportMac = FormatMacReversed(s.Slice(offset, 6));
                offset += 6;

                int dataLen = s[offset++];
                if (offset + dataLen + 1 > s.Length)
                    return Task.CompletedTask;

                var adData = s.Slice(offset, dataLen);
                offset += dataLen;

                short rssi = unchecked((sbyte)s[offset++]);

                if (TryParseMeasurement(adData, reportMac, rssi) is {} sample)
                {
                    return _sampleArrived(sample, ct);
                }
            }
            
            return Task.CompletedTask;
        }

        Task ParseExtendedAdvertisingReports(ReadOnlySpan<byte> s)
        {
            // Format:
            // [0]=0x04 [1]=0x3E [2]=paramLen [3]=0x0D [4]=numReports ...
            if (s.Length < 5)
                return Task.CompletedTask;;

            int offset = 4;
            int numReports = s[offset++];

            for (int i = 0; i < numReports; i++)
            {
                // Extended Advertising Report layout:
                // event_type(2)
                // addr_type(1)
                // addr(6)
                // primary_phy(1)
                // secondary_phy(1)
                // sid(1)
                // tx_power(1)
                // rssi(1)
                // periodic_adv_interval(2)
                // direct_addr_type(1)
                // direct_addr(6)
                // data_len(1)
                // data(N)
                const int HeaderLenBeforeDataLen = 24; // bytes after report start up to and including direct_addr
                if (offset + HeaderLenBeforeDataLen + 1 > s.Length)
                    return Task.CompletedTask;;

                ushort eventType = ReadUInt16LE(s, offset);
                offset += 2;

                byte addrType = s[offset++];
                _ = eventType;
                _ = addrType;

                string reportMac = FormatMacReversed(s.Slice(offset, 6));
                offset += 6;

                byte primaryPhy = s[offset++];
                byte secondaryPhy = s[offset++];
                byte sid = s[offset++];
                sbyte txPower = unchecked((sbyte)s[offset++]);
                short rssi = unchecked((sbyte)s[offset++]);
                ushort periodicAdvInterval = ReadUInt16LE(s, offset);
                offset += 2;
                byte directAddrType = s[offset++];
                offset += 6; // direct address

                _ = primaryPhy;
                _ = secondaryPhy;
                _ = sid;
                _ = txPower;
                _ = periodicAdvInterval;
                _ = directAddrType;

                int dataLen = s[offset++];
                if (offset + dataLen > s.Length)
                    return Task.CompletedTask;

                var adData = s.Slice(offset, dataLen);
                offset += dataLen;

                if (TryParseMeasurement(adData, reportMac, rssi) is { } sample)
                {
                    return _sampleArrived(sample, ct);
                }
            }
            
            return Task.CompletedTask;;
        }

        Sample? TryParseMeasurement(ReadOnlySpan<byte> adData, string reportMac, short rssi)
        {
            int offset = 0;

            while (offset < adData.Length)
            {
                int elementLen = adData[offset];
                offset++;

                if (elementLen == 0)
                    break;

                if (offset + elementLen > adData.Length)
                    break;

                byte adType = adData[offset];
                var elementData = adData.Slice(offset + 1, elementLen - 1);
                offset += elementLen;

                // 0x16 = Service Data - 16-bit UUID
                if (adType != 0x16)
                    continue;

                if (elementData.Length < 2)
                    continue;

                ushort uuid16 = ReadUInt16LE(elementData, 0);
                if (uuid16 != EnvironmentalSensingUuid16)
                    continue;

                var payload = elementData.Slice(2);
                if (payload.Length != 15)
                    continue;

                string payloadMac = FormatMacReversed(payload.Slice(0, 6));

                // Optional consistency check:
                // If report header MAC and payload MAC disagree, ignore packet.
                if (!string.Equals(reportMac, payloadMac, StringComparison.OrdinalIgnoreCase))
                    continue;

                short tempRaw = ReadInt16LE(payload, 6);
                ushort humRaw = ReadUInt16LE(payload, 8);
                ushort battMv = ReadUInt16LE(payload, 10);
                byte battPct = payload[12];

                double temperature = tempRaw / 100.0;
                double humidity = humRaw / 100.0;

                var sample = new Sample(
                    Mac: payloadMac,
                    Rssi: rssi,
                    Temperature: temperature,
                    Humidity: humidity,
                    BattMv: battMv,
                    BattPct: battPct);

                return sample;
            }

            return null;
        }

        static ushort ReadUInt16LE(ReadOnlySpan<byte> s, int offset)
            => (ushort)(s[offset] | (s[offset + 1] << 8));

        static short ReadInt16LE(ReadOnlySpan<byte> s, int offset)
            => unchecked((short)(s[offset] | (s[offset + 1] << 8)));

        static string FormatMacReversed(ReadOnlySpan<byte> addrLe)
        {
            // HCI and payload nose MAC little-endian, user-friendly prikaz je obrnuto.
            return string.Create(17, addrLe.ToArray(), static (chars, bytes) =>
            {
                int pos = 0;
                for (int i = bytes.Length - 1; i >= 0; i--)
                {
                    byte b = bytes[i];
                    chars[pos++] = GetHex((b >> 4) & 0xF);
                    chars[pos++] = GetHex(b & 0xF);
                    if (i != 0)
                        chars[pos++] = ':';
                }

                static char GetHex(int value)
                    => (char)(value < 10 ? '0' + value : 'A' + (value - 10));
            });
        }
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
