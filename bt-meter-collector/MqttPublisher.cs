using System.Collections.Concurrent;
using System.Text.Json;
using MQTTnet;

namespace bt_meter_collector;

internal sealed class MqttPublisher
{
    private readonly ILogger _logger;
    private readonly MqttClientOptions _options;
    private IMqttClient? _client;

    private readonly ConcurrentDictionary<string, DateTime> _lastPublishedAt = new();

    public MqttPublisher(
        ILogger logger,
        MqttConfiguration mqttConfiguration)
    {
        _logger = logger;
        _options = new MqttClientOptionsBuilder()
            .WithTcpServer(mqttConfiguration.Host, mqttConfiguration.Port)
            .Build();
    }

    public async Task PublishSampleAsync(Sample sample, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        if (_lastPublishedAt.TryGetValue(sample.Mac, out var lastPublishedAt) &&
            now - lastPublishedAt < TimeSpan.FromMinutes(1))
        {
            _logger.LogDebug("[PublishSample] Skipped, published less than 1 minute ago for {Mac}", sample.Mac);
            return;
        }

        var client = await GrabConnectedClientAsync(cancellationToken);
        var filteredMac = sample.Mac.Replace(":", string.Empty);

        await client.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic($"rumenka/sensor/{filteredMac}/state")
                .WithPayload(JsonSerializer.Serialize(new
                {
                    rssi = sample.Rssi,
                    temperature = sample.Temperature,
                    humidity = sample.Humidity,
                    battery = sample.BattPct
                }))
                .Build(),
            cancellationToken);

        _lastPublishedAt[sample.Mac] = now;

        _logger.LogInformation("[PublishSample] Mqtt message sent: {@Message}", sample);
    }

    private async Task<IMqttClient> GrabConnectedClientAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            return _client;
        }

        var mqttFactory = new MqttClientFactory();
        _client = mqttFactory.CreateMqttClient();
        await _client.ConnectAsync(_options, cancellationToken);
        if (!_client.IsConnected)
        {
            throw new IOException("Not able to connect to MQTT broker");
        }

        return _client;
    }
}
