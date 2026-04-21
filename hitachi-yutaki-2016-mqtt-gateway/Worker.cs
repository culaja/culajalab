using System.Collections.Concurrent;
using System.Text.Json;
using FluentModbus;
using MQTTnet;

namespace hitachi_yutaki_2016_mqtt_gateway;

internal sealed class Worker : BackgroundService
{
    private const string TopicBase = "rumenka/hitachi/yutaki2016";
    private const string ControlTopicPrefix = "rumenka/hitachi/yutaki2016/control";

    private static readonly IReadOnlyDictionary<RegisterGroup, string> GroupTopics = new Dictionary<RegisterGroup, string>
    {
        [RegisterGroup.Unit]        = $"{TopicBase}/unit",
        [RegisterGroup.Circuit1]    = $"{TopicBase}/circuit1",
        [RegisterGroup.Circuit2]    = $"{TopicBase}/circuit2",
        [RegisterGroup.Dhw]         = $"{TopicBase}/dhw",
        [RegisterGroup.Pool]        = $"{TopicBase}/pool",
        [RegisterGroup.Diagnostics] = $"{TopicBase}/diagnostics",
    };

    private readonly ILogger<Worker> _logger;
    private readonly ModbusConfiguration _modbusConfig;
    private readonly MqttConfiguration _mqttConfig;
    private readonly GatewayConfiguration _gatewayConfig;

    private readonly Dictionary<string, string> _lastPayload = new();
    private readonly Dictionary<RegisterGroup, DateTime> _lastPublishedAt =
        System.Enum.GetValues<RegisterGroup>().ToDictionary(g => g, _ => DateTime.MinValue);
    private readonly ConcurrentQueue<(RegisterDefinition Reg, short Value)> _pendingWrites = new();

    private ModbusTcpClient? _modbusClient;

    public Worker(ILogger<Worker> logger, ModbusConfiguration modbusConfig, MqttConfiguration mqttConfig, GatewayConfiguration gatewayConfig)
    {
        _logger = logger;
        _modbusConfig = modbusConfig;
        _mqttConfig = mqttConfig;
        _gatewayConfig = gatewayConfig;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mqttClient = await ConnectMqttAsync(stoppingToken);
        await SubscribeControlTopicsAsync(mqttClient, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_modbusClient is null)
                {
                    _modbusClient = new ModbusTcpClient();
                    var addresses = await System.Net.Dns.GetHostAddressesAsync(_modbusConfig.Host, stoppingToken);
                    _modbusClient.Connect(new System.Net.IPEndPoint(addresses[0], _modbusConfig.Port), ModbusEndianness.BigEndian);
                    _logger.LogInformation("Connected to Modbus at {Host}:{Port}", _modbusConfig.Host, _modbusConfig.Port);
                }

                ProcessPendingWrites();

                var block1 = _modbusClient.ReadHoldingRegisters<short>(_modbusConfig.UnitId, 1000, 34).ToArray();
                var block2 = _modbusClient.ReadHoldingRegisters<short>(_modbusConfig.UnitId, 1050, 49).ToArray();
                var block3 = _modbusClient.ReadHoldingRegisters<short>(_modbusConfig.UnitId, 1200, 32).ToArray();

                var groupSnapshots = System.Enum.GetValues<RegisterGroup>()
                    .ToDictionary(g => g, _ => new Dictionary<string, object?>());
                var changedGroups = new HashSet<RegisterGroup>();

                foreach (var reg in RegisterDefinitions.All)
                {
                    var raw = GetRaw(block1, block2, block3, reg.Address);
                    var snapshot = groupSnapshots[reg.Group];

                    if (reg.Kind == RegisterKind.Bitmask)
                    {
                        var prefix = reg.BitmaskPrefix ?? (reg.Name + "_");
                        foreach (var kvp in reg.BitNames!)
                            snapshot[$"{prefix}{kvp.Value}"] = (raw & (1 << kvp.Key)) != 0;

                        var rawStr = raw.ToString();
                        if (HasChanged(reg.Name, rawStr))
                        {
                            changedGroups.Add(reg.Group);
                            _lastPayload[reg.Name] = rawStr;
                        }
                    }
                    else
                    {
                        var (payload, numericValue) = Decode(reg, raw);
                        snapshot[reg.Name] = numericValue.HasValue ? numericValue.Value : payload;

                        if (HasChanged(reg.Name, payload))
                        {
                            changedGroups.Add(reg.Group);
                            _lastPayload[reg.Name] = payload;
                        }
                    }
                }

                var now = DateTime.UtcNow;

                foreach (var group in System.Enum.GetValues<RegisterGroup>())
                {
                    var forcePublish = now - _lastPublishedAt[group] >= _gatewayConfig.ForcePublishInterval;

                    if (!changedGroups.Contains(group) && !forcePublish)
                        continue;

                    await mqttClient.PublishAsync(
                        new MqttApplicationMessageBuilder()
                            .WithTopic(GroupTopics[group])
                            .WithPayload(JsonSerializer.Serialize(groupSnapshots[group]))
                            .Build(),
                        stoppingToken);

                    _lastPublishedAt[group] = now;
                    _logger.LogInformation("{Group} published ({Reason})",
                        group, changedGroups.Contains(group) ? "change" : "force");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Poll failed, will reconnect on next cycle");
                _modbusClient?.Dispose();
                _modbusClient = null;
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _modbusClient?.Dispose();
        await mqttClient.DisconnectAsync(cancellationToken: CancellationToken.None);
    }

    private void ProcessPendingWrites()
    {
        while (_pendingWrites.TryDequeue(out var write))
        {
            try
            {
                _modbusClient!.WriteSingleRegister(_modbusConfig.UnitId, write.Reg.Address, write.Value);
                _logger.LogInformation("Wrote {Value} to {Register}", write.Value, write.Reg.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write {Value} to {Register}", write.Value, write.Reg.Name);
            }
        }
    }

    private bool HasChanged(string name, string payload) =>
        !_lastPayload.TryGetValue(name, out var last) || payload != last;

    private static short GetRaw(short[] block1, short[] block2, short[] block3, ushort address)
    {
        if (address is >= 1000 and <= 1033) return block1[address - 1000];
        if (address is >= 1050 and <= 1098) return block2[address - 1050];
        if (address is >= 1200 and <= 1231) return block3[address - 1200];
        throw new InvalidOperationException($"Unknown register address: {address}");
    }

    private static (string payload, double? numericValue) Decode(RegisterDefinition reg, short raw)
    {
        switch (reg.Kind)
        {
            case RegisterKind.Enum:
                var label = reg.EnumValues!.TryGetValue(raw, out var s) ? s : $"Unknown({raw})";
                return (label, null);

            case RegisterKind.Analog:
                var value = raw * reg.Scale;
                return (value.ToString("G6"), value);

            case RegisterKind.Bitmask:
                var bits = reg.BitNames!.ToDictionary(
                    kvp => kvp.Value,
                    kvp => (raw & (1 << kvp.Key)) != 0);
                return (JsonSerializer.Serialize(bits), null);

            case RegisterKind.Raw:
                return (raw.ToString(), null);

            default:
                throw new InvalidOperationException($"Unknown register kind: {reg.Kind}");
        }
    }

    private async Task<IMqttClient> ConnectMqttAsync(CancellationToken ct)
    {
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_mqttConfig.Host, _mqttConfig.Port)
            .Build();

        var client = new MqttClientFactory().CreateMqttClient();
        await client.ConnectAsync(options, ct);
        _logger.LogInformation("Connected to MQTT broker at {Host}:{Port}", _mqttConfig.Host, _mqttConfig.Port);
        return client;
    }

    private async Task SubscribeControlTopicsAsync(IMqttClient client, CancellationToken ct)
    {
        var writableRegisters = RegisterDefinitions.All.Where(r => r.IsWritable).ToList();

        await client.SubscribeAsync(
            new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter($"{ControlTopicPrefix}/+")
                .Build(),
            ct);

        client.ApplicationMessageReceivedAsync += args =>
        {
            var topic = args.ApplicationMessage.Topic;
            var registerName = topic.Replace($"{ControlTopicPrefix}/", string.Empty);
            var payload = args.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;

            if (!RegisterDefinitions.ByName.TryGetValue(registerName, out var reg) || !reg.IsWritable)
            {
                _logger.LogWarning("Received write request for unknown or read-only register: {Register}", registerName);
                return Task.CompletedTask;
            }

            if (!TryParseWriteValue(reg, payload, out var rawValue))
            {
                _logger.LogWarning("Could not parse write value '{Payload}' for register {Register}", payload, registerName);
                return Task.CompletedTask;
            }

            _pendingWrites.Enqueue((reg, rawValue));
            return Task.CompletedTask;
        };

        _logger.LogInformation("Subscribed to control topics for {Count} writable registers", writableRegisters.Count);
    }

    private static bool TryParseWriteValue(RegisterDefinition reg, string payload, out short rawValue)
    {
        if (short.TryParse(payload, out rawValue))
            return true;

        if (reg.Kind == RegisterKind.Enum && reg.EnumValues is not null)
        {
            var match = reg.EnumValues.FirstOrDefault(kvp =>
                string.Equals(kvp.Value, payload, StringComparison.OrdinalIgnoreCase));
            if (match.Value is not null)
            {
                rawValue = (short)match.Key;
                return true;
            }
        }

        if (reg.Kind == RegisterKind.Analog && double.TryParse(payload, out var engValue))
        {
            rawValue = (short)Math.Round(engValue / reg.Scale);
            return true;
        }

        rawValue = 0;
        return false;
    }
}
