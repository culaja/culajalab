using System.Collections.Concurrent;
using System.Text.Json;
using MQTTnet;

namespace bt_meter_collector;

internal sealed class HomeAssistantSensorMqttPublisher
{
    private readonly ILogger _logger;
    private readonly MqttClientOptions _options;
    private IMqttClient? _client;
    
    private readonly ConcurrentDictionary<string, DateTime> _lastPublishedAt = new();

    public HomeAssistantSensorMqttPublisher(
        ILogger logger,
        MqttConfiguration mqttConfiguration)
    {
        _logger = logger;
        _options = new MqttClientOptionsBuilder()
            .WithTcpServer(mqttConfiguration.Host, mqttConfiguration.Port)
            .Build();
    }
    
    public async Task RegisterSensorAsync(string mac, CancellationToken cancellationToken)
    {
        var client = await GrabConnectedClientAsync(cancellationToken);
        
        var filteredMac = mac.Replace(":", string.Empty);
        
        var rssiConfigMessage = new
        {
            name = "RSSI",
            state_topic = $"rumenka/sensor/{filteredMac}/state",
            unit_of_measurement = "dBm",
            device_class = "signal_strength",
            value_template = "{{ value_json.rssi }}",
            state_class = "measurement",
            unique_id = $"sensor_{filteredMac}_rssi",
            device = new
            {
                identifiers = new[] { filteredMac },
                model = "LYWSD03MMC (PVVX)",
                manufacturer = "Xiaomi"
            }
        };
        
        await client.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/sensor/{filteredMac}_rssi/config")
                .WithPayload(JsonSerializer.Serialize(rssiConfigMessage))
                .WithRetainFlag()
                .Build(),
            cancellationToken);
        
        _logger.LogInformation("[RegisterSensor] Mqtt message sent: {@Message}", rssiConfigMessage);
        
        var temperatureConfigMessage = new
        {
            name = "Temperature",
            state_topic = $"rumenka/sensor/{filteredMac}/state",
            unit_of_measurement = "°C",
            device_class = "temperature",
            value_template = "{{ value_json.temperature }}",
            state_class = "measurement",
            unique_id = $"sensor_{filteredMac}_temperature",
            device = new
            {
                identifiers = new[] { filteredMac },
                model = "LYWSD03MMC (PVVX)",
                manufacturer = "Xiaomi"
            }
        };
        
        await client.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/sensor/{filteredMac}_temperature/config")
                .WithPayload(JsonSerializer.Serialize(temperatureConfigMessage))
                .WithRetainFlag()
                .Build(),
            cancellationToken);
        
        _logger.LogInformation("[RegisterSensor] Mqtt message sent: {@Message}", temperatureConfigMessage);
        
        var humidityConfigMessage = new
        {
            name = "Humidity",
            state_topic = $"rumenka/sensor/{filteredMac}/state",
            unit_of_measurement = "%",
            device_class = "humidity",
            value_template = "{{ value_json.humidity }}",
            state_class = "measurement",
            unique_id = $"sensor_{filteredMac}_humidity",
            device = new
            {
                identifiers = new[] { filteredMac },
                model = "LYWSD03MMC (PVVX)",
                manufacturer = "Xiaomi"
            }
        };
        
        await client.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/sensor/{filteredMac}_humidity/config")
                .WithPayload(JsonSerializer.Serialize(humidityConfigMessage))
                .WithRetainFlag()
                .Build(),
            cancellationToken);
        
        _logger.LogInformation("[RegisterSensor] Mqtt message sent: {@Message}", humidityConfigMessage);
        
        var batteryConfigMessage = new
        {
            name = "Battery",
            state_topic = $"rumenka/sensor/{filteredMac}/state",
            unit_of_measurement = "%",
            device_class = "battery",
            value_template = "{{ value_json.battery }}",
            state_class = "measurement",
            unique_id = $"sensor_{filteredMac}_battery",
            device = new
            {
                identifiers = new[] { filteredMac },
                model = "LYWSD03MMC (PVVX)",
                manufacturer = "Xiaomi"
            }
        };
        
        await client.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/sensor/{filteredMac}_battery/config")
                .WithPayload(JsonSerializer.Serialize(batteryConfigMessage))
                .WithRetainFlag()
                .Build(),
            cancellationToken);
        
        _logger.LogInformation("[RegisterSensor] Mqtt message sent: {@Message}", batteryConfigMessage);
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