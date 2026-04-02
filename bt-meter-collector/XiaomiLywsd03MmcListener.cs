using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

public sealed record Sample(string Mac, short Rssi, double Temperature, double Humidity, ushort BattMv, byte BattPct);

public sealed record BleAdvertisement(string Mac, short? Rssi, string ServiceUuid, byte[] Data);

internal sealed class XiaomiLywsd03MmcListener : IAsyncDisposable
{
    private readonly Func<string, CancellationToken, Task> _deviceAppeared;
    private readonly Func<Sample, CancellationToken, Task> _sampleArrived;

    private readonly ConcurrentDictionary<string, bool> _registeredMacs = new();

    private Process? _process;
    private Task? _stdoutTask;
    private Task? _stderrTask;

    public XiaomiLywsd03MmcListener(
        Func<string, CancellationToken, Task> deviceAppeared,
        Func<Sample, CancellationToken, Task> sampleArrived)
    {
        _deviceAppeared = deviceAppeared;
        _sampleArrived = sampleArrived;
    }

    public Task StartListeningAsync(CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "btmon",
            Arguments = "-i hci0",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        if (!_process.Start())
            throw new InvalidOperationException("Failed to start btmon.");

        _stdoutTask = Task.Run(() => ReadStdoutAsync(_process.StandardOutput, cancellationToken), cancellationToken);
        _stderrTask = Task.Run(() => ReadStderrAsync(_process.StandardError, cancellationToken), cancellationToken);

        cancellationToken.Register(() =>
        {
            try
            {
                if (_process is { HasExited: false })
                    _process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        });

        return Task.CompletedTask;
    }

    private async Task ReadStdoutAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        BtMonPacketBuilder? current = null;

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var rawLine = await reader.ReadLineAsync(cancellationToken);
            if (rawLine is null)
                break;

            var line = rawLine.TrimEnd();

            if (line.StartsWith("> HCI Event:", StringComparison.Ordinal))
            {
                await FlushCurrentAsync(current, cancellationToken);
                current = new BtMonPacketBuilder();
                continue;
            }

            if (current is null)
                continue;

            if (TryParseAddress(line, out var mac))
            {
                // Ako u jednom HCI eventu dođe novi entry, flush prethodnog ako ima service data.
                if (current.HasUsefulPayload)
                {
                    await FlushCurrentAsync(current, cancellationToken);
                    current = new BtMonPacketBuilder();
                }

                current.Mac = mac;
                continue;
            }

            if (TryParseRssi(line, out var rssi))
            {
                current.Rssi = rssi;
                continue;
            }

            if (TryParseServiceDataHeader(line, out var serviceUuid))
            {
                current.ServiceUuid = serviceUuid;
                current.DataHex.Clear();
                continue;
            }

            if (TryParseDataLine(line, out var dataHex))
            {
                current.DataHex.Append(dataHex);
                continue;
            }
        }

        await FlushCurrentAsync(current, cancellationToken);
    }

    private static bool TryParseAddress(string line, out string mac)
    {
        var m = Regex.Match(line, @"^\s*Address:\s+([0-9A-F]{2}(:[0-9A-F]{2}){5})\b", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            mac = m.Groups[1].Value.ToUpperInvariant();
            return true;
        }

        mac = string.Empty;
        return false;
    }

    private static bool TryParseRssi(string line, out short rssi)
    {
        var m = Regex.Match(line, @"^\s*RSSI:\s+(-?\d+)\s+dBm\b", RegexOptions.IgnoreCase);
        if (m.Success && short.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out rssi))
            return true;

        rssi = 0;
        return false;
    }

    private static bool TryParseServiceDataHeader(string line, out string serviceUuid)
    {
        // Primjeri:
        // Service Data: Unknown (0xa201)
        // Service Data: Google (0xfef3)
        var m = Regex.Match(line, @"^\s*Service Data:\s+.+\((0x[0-9a-fA-F]+)\)\s*$");
        if (m.Success)
        {
            serviceUuid = m.Groups[1].Value[2..].ToLowerInvariant();
            return true;
        }

        serviceUuid = string.Empty;
        return false;
    }

    private static bool TryParseDataLine(string line, out string hex)
    {
        var m = Regex.Match(line, @"^\s*Data:\s+([0-9a-fA-F]+)\s*$");
        if (m.Success)
        {
            hex = m.Groups[1].Value;
            return true;
        }

        hex = string.Empty;
        return false;
    }

    private async Task FlushCurrentAsync(BtMonPacketBuilder? current, CancellationToken cancellationToken)
    {
        if (current is null || !current.IsComplete)
            return;

        byte[] data;
        try
        {
            data = Convert.FromHexString(current.DataHex.ToString());
        }
        catch
        {
            return;
        }

        var adv = new BleAdvertisement(current.Mac!, current.Rssi, current.ServiceUuid!, data);

        if (!_registeredMacs.ContainsKey(adv.Mac))
        {
            await _deviceAppeared(adv.Mac, cancellationToken);
            _registeredMacs[adv.Mac] = true;
        }

        await TryHandleKnownAdvertisementAsync(adv, cancellationToken);
    }

    private async Task TryHandleKnownAdvertisementAsync(BleAdvertisement adv, CancellationToken cancellationToken)
    {
        // Tvoj stari PVVX parser
        if (adv.ServiceUuid == "181a" && adv.Data.Length >= 13 && adv.Rssi.HasValue)
        {
            var parsed = TryParsePvvx181A(adv.Mac, adv.Rssi.Value, adv.Data);
            if (parsed is not null)
                await _sampleArrived(parsed, cancellationToken);

            return;
        }

        // Ovdje trenutno samo logujemo nepoznato; ovo je korisno za tvoj 0xa201 slučaj
        Console.WriteLine(
            $"BLE adv mac={adv.Mac} rssi={adv.Rssi?.ToString() ?? "?"} uuid=0x{adv.ServiceUuid} len={adv.Data.Length} data={Convert.ToHexString(adv.Data)}");
    }

    private static Sample? TryParsePvvx181A(string mac, short rssi, byte[] d)
    {
        // Stari format koji si već imao.
        // Ostavio sam ga da ne izgubiš kompatibilnost ako se taj firmware ipak pojavi.
        if (d.Length < 13)
            return null;

        try
        {
            short tempRaw = BitConverter.ToInt16(d, 6);
            ushort humRaw = BitConverter.ToUInt16(d, 8);
            ushort battMv = BitConverter.ToUInt16(d, 10);
            byte battPct = d[12];

            return new Sample(
                mac,
                rssi,
                tempRaw / 100.0,
                humRaw / 100.0,
                battMv,
                battPct);
        }
        catch
        {
            return null;
        }
    }

    private static async Task ReadStderrAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(line))
                Console.WriteLine($"btmon: {line}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_process is { HasExited: false })
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
        }

        if (_stdoutTask is not null)
            await SafeAwait(_stdoutTask);

        if (_stderrTask is not null)
            await SafeAwait(_stderrTask);

        _process?.Dispose();
    }

    private static async Task SafeAwait(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
        }
    }

    private sealed class BtMonPacketBuilder
    {
        public string? Mac { get; set; }
        public short? Rssi { get; set; }
        public string? ServiceUuid { get; set; }
        public StringBuilder DataHex { get; } = new();

        public bool HasUsefulPayload => !string.IsNullOrWhiteSpace(ServiceUuid) && DataHex.Length > 0;

        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(Mac) &&
            !string.IsNullOrWhiteSpace(ServiceUuid) &&
            DataHex.Length > 0;
    }
}