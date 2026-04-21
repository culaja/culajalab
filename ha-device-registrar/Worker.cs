using System.Text.Json;
using MQTTnet;

namespace ha_device_registrar;

internal sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly MqttConfiguration _mqttConfiguration;
    private readonly RegistrarConfiguration _registrarConfig;
    private readonly List<DeviceConfig> _devices;

    public Worker(ILogger<Worker> logger, MqttConfiguration mqttConfiguration, RegistrarConfiguration registrarConfig, List<DeviceConfig> devices)
    {
        _logger = logger;
        _mqttConfiguration = mqttConfiguration;
        _registrarConfig = registrarConfig;
        _devices = devices;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_mqttConfiguration.Host, _mqttConfiguration.Port)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(15))
            .Build();

        var brokerDisconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var client = new MqttClientFactory().CreateMqttClient();
        client.DisconnectedAsync += args =>
        {
            if (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("MQTT broker disconnected unexpectedly.");
                brokerDisconnected.TrySetException(new IOException("MQTT broker disconnected."));
            }
            return Task.CompletedTask;
        };

        await client.ConnectAsync(options, stoppingToken);
        _logger.LogInformation("Connected to MQTT broker at {Host}:{Port}", _mqttConfiguration.Host, _mqttConfiguration.Port);

        foreach (var device in _devices)
        {
            await RegisterDeviceAsync(client, device, stoppingToken);
        }

        _logger.LogInformation("All devices registered. Running indefinitely.");

        await Task.WhenAny(brokerDisconnected.Task, Task.Delay(Timeout.Infinite, stoppingToken));

        if (brokerDisconnected.Task.IsFaulted)
            await brokerDisconnected.Task;

        await client.DisconnectAsync(cancellationToken: CancellationToken.None);
    }

    private async Task RegisterDeviceAsync(IMqttClient client, DeviceConfig device, CancellationToken ct)
    {
        var filteredMac = device.Mac.Replace(":", string.Empty);
        var stateTopic = device.StateTopic.Replace("{mac}", filteredMac);
        var deviceIdentifier = new { identifiers = new[] { filteredMac }, model = device.Model, manufacturer = device.Manufacturer };

        foreach (var sensor in device.Sensors)
        {
            var sensorId = sensor.Name.ToLowerInvariant().Replace(" ", "_");
            var uniqueId = $"sensor_{filteredMac}_{sensorId}";

            var config = new
            {
                name = sensor.Name,
                state_topic = stateTopic,
                unit_of_measurement = sensor.UnitOfMeasurement,
                device_class = sensor.DeviceClass,
                value_template = sensor.ValueTemplate,
                state_class = sensor.StateClass,
                expire_after = _registrarConfig.SensorExpirySeconds,
                unique_id = uniqueId,
                device = deviceIdentifier
            };

            await client.PublishAsync(
                new MqttApplicationMessageBuilder()
                    .WithTopic($"homeassistant/sensor/{uniqueId}/config")
                    .WithPayload(JsonSerializer.Serialize(config))
                    .WithRetainFlag()
                    .Build(),
                ct);

            _logger.LogInformation("Registered sensor {UniqueId}", uniqueId);
        }
    }
}
