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

        Console.WriteLine("Starting btmon process...");

        _process = new Process
        {
            StartInfo = psi,
            EnableRaisingEvents = true
        };

        if (!_process.Start())
            throw new InvalidOperationException("Failed to start btmon.");

        Console.WriteLine($"btmon started. PID={_process.Id}");

        _stdoutTask = Task.Run(() => ReadStdoutAsync(_process.StandardOutput, cancellationToken), cancellationToken);
        _stderrTask = Task.Run(() => ReadStderrAsync(_process.StandardError, cancellationToken), cancellationToken);

        cancellationToken.Register(() =>
        {
            try
            {
                Console.WriteLine("Cancellation requested, stopping btmon...");
                if (_process is { HasExited: false })
                    _process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to stop btmon: {ex}");
            }
        });

        return Task.CompletedTask;
    }

    private async Task ReadStdoutAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        Console.WriteLine("BTMON STDOUT reader started");

        BtMonPacketBuilder? current = null;
        int lineCount = 0;

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            string? rawLine;
            try
            {
                rawLine = await reader.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("STDOUT read cancelled");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"STDOUT read failed: {ex}");
                break;
            }

            if (rawLine is null)
            {
                Console.WriteLine("STDOUT EOF reached");
                break;
            }

            lineCount++;
            var line = rawLine.TrimEnd();

            if (lineCount <= 20)
                Console.WriteLine($"LINE[{lineCount}]: {line}");
            else if (lineCount % 100 == 0)
                Console.WriteLine($"Read {lineCount} lines from btmon");

            if (line.StartsWith("> HCI Event:", StringComparison.Ordinal))
            {
                Console.WriteLine("NEW HCI EVENT");

                await FlushCurrentAsync(current, cancellationToken);
                current = new BtMonPacketBuilder();
                continue;
            }

            if (current is null)
                continue;

            if (TryParseAddress(line, out var mac))
            {
                Console.WriteLine($"MAC: {mac}");

                if (current.HasUsefulPayload)
                {
                    Console.WriteLine("Flushing previous packet before new MAC");
                    await FlushCurrentAsync(current, cancellationToken);
                    current = new BtMonPacketBuilder();
                    current.Mac = mac;
                    continue;
                }

                current.Mac = mac;
                continue;
            }

            if (TryParseRssi(line, out var rssi))
            {
                Console.WriteLine($"RSSI: {rssi}");
                current.Rssi = rssi;
                continue;
            }

            if (TryParseServiceDataHeader(line, out var serviceUuid))
            {
                Console.WriteLine($"SERVICE UUID: 0x{serviceUuid}");
                current.ServiceUuid = serviceUuid;
                current.DataHex.Clear();
                continue;
            }

            if (TryParseDataLine(line, out var dataHex))
            {
                Console.WriteLine($"DATA: {dataHex}");
                current.DataHex.Append(dataHex);
                continue;
            }
        }

        Console.WriteLine("STDOUT loop ended");
        await FlushCurrentAsync(current, cancellationToken);
    }

    private static bool TryParseAddress(string line, out string mac)
    {
        var m = Regex.Match(
            line,
            @"^\s*Address:\s+([0-9A-F]{2}(:[0-9A-F]{2}){5})\b",
            RegexOptions.IgnoreCase);

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
        var m = Regex.Match(
            line,
            @"^\s*RSSI:\s+(-?\d+)\s+dBm\b",
            RegexOptions.IgnoreCase);

        if (m.Success &&
            short.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out rssi))
        {
            return true;
        }

        rssi = 0;
        return false;
    }

    private static bool TryParseServiceDataHeader(string line, out string serviceUuid)
    {
        var m = Regex.Match(
            line,
            @"^\s*Service Data:\s+.+\((0x[0-9a-fA-F]+)\)\s*$",
            RegexOptions.IgnoreCase);

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
        var m = Regex.Match(
            line,
            @"^\s*Data:\s+([0-9a-fA-F]+)\s*$",
            RegexOptions.IgnoreCase);

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
        if (current is null)
        {
            Console.WriteLine("Flush skipped: current is null");
            return;
        }

        if (!current.IsComplete)
        {
            Console.WriteLine(
                $"Flush skipped: incomplete packet mac={current.Mac ?? "<null>"} uuid={current.ServiceUuid ?? "<null>"} dataHexLen={current.DataHex.Length}");
            return;
        }

        Console.WriteLine(
            $"FLUSH: mac={current.Mac} rssi={current.Rssi?.ToString() ?? "?"} uuid=0x{current.ServiceUuid} dataHexLen={current.DataHex.Length}");

        byte[] data;
        try
        {
            data = Convert.FromHexString(current.DataHex.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HEX parse failed: {ex}");
            return;
        }

        Console.WriteLine($"PARSED DATA LEN: {data.Length}");

        var adv = new BleAdvertisement(current.Mac!, current.Rssi, current.ServiceUuid!, data);

        if (!_registeredMacs.ContainsKey(adv.Mac))
        {
            Console.WriteLine($"DEVICE APPEARED: {adv.Mac}");
            await _deviceAppeared(adv.Mac, cancellationToken);
            _registeredMacs[adv.Mac] = true;
        }

        await TryHandleKnownAdvertisementAsync(adv, cancellationToken);
    }

    private async Task TryHandleKnownAdvertisementAsync(BleAdvertisement adv, CancellationToken cancellationToken)
    {
        Console.WriteLine(
            $"ADV RECEIVED: mac={adv.Mac} rssi={adv.Rssi?.ToString() ?? "?"} uuid=0x{adv.ServiceUuid} len={adv.Data.Length} data={Convert.ToHexString(adv.Data)}");

        if (adv.ServiceUuid == "181a")
        {
            Console.WriteLine("Trying PVVX 0x181a parser...");
            if (!adv.Rssi.HasValue)
            {
                Console.WriteLine("Skipping 181a packet because RSSI is missing");
                return;
            }

            var parsed = TryParsePvvx181A(adv.Mac, adv.Rssi.Value, adv.Data);
            if (parsed is not null)
            {
                Console.WriteLine(
                    $"PVVX PARSED OK: mac={parsed.Mac} temp={parsed.Temperature} hum={parsed.Humidity} battMv={parsed.BattMv} battPct={parsed.BattPct}");
                await _sampleArrived(parsed, cancellationToken);
            }
            else
            {
                Console.WriteLine("PVVX parser returned null");
            }

            return;
        }

        Console.WriteLine($"No parser for service uuid 0x{adv.ServiceUuid}");
    }

    private static Sample? TryParsePvvx181A(string mac, short rssi, byte[] d)
    {
        try
        {
            if (d.Length < 13)
            {
                Console.WriteLine($"PVVX data too short: {d.Length}");
                return null;
            }

            short tempRaw = BitConverter.ToInt16(d, 6);
            ushort humRaw = BitConverter.ToUInt16(d, 8);
            ushort battMv = BitConverter.ToUInt16(d, 10);
            byte battPct = d[12];

            double temp = tempRaw / 100.0;
            double hum = humRaw / 100.0;

            return new Sample(mac, rssi, temp, hum, battMv, battPct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PVVX parse exception: {ex}");
            return null;
        }
    }

    private static async Task ReadStderrAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        Console.WriteLine("BTMON STDERR reader started");

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("STDERR read cancelled");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"STDERR read failed: {ex}");
                break;
            }

            if (line is null)
                break;

            if (!string.IsNullOrWhiteSpace(line))
                Console.WriteLine($"btmon ERR: {line}");
        }

        Console.WriteLine("BTMON STDERR reader ended");
    }

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine("Disposing listener...");

        try
        {
            if (_process is { HasExited: false })
                _process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Dispose kill failed: {ex}");
        }

        if (_stdoutTask is not null)
            await SafeAwait(_stdoutTask);

        if (_stderrTask is not null)
            await SafeAwait(_stderrTask);

        _process?.Dispose();

        Console.WriteLine("Listener disposed");
    }

    private static async Task SafeAwait(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Background task failed: {ex}");
        }
    }

    private sealed class BtMonPacketBuilder
    {
        public string? Mac { get; set; }
        public short? Rssi { get; set; }
        public string? ServiceUuid { get; set; }
        public StringBuilder DataHex { get; } = new();

        public bool HasUsefulPayload =>
            !string.IsNullOrWhiteSpace(ServiceUuid) && DataHex.Length > 0;

        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(Mac) &&
            !string.IsNullOrWhiteSpace(ServiceUuid) &&
            DataHex.Length > 0;
    }
}