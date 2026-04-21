using System.Text.Json;
using MQTTnet;

namespace ha_device_registrar;

internal sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly MqttConfiguration _mqttConfiguration;
    private readonly RegistrarConfiguration _registrarConfig;
    private readonly List<DeviceConfig> _devices;
    private readonly List<ClimateDeviceConfig> _climates;

    public Worker(ILogger<Worker> logger, MqttConfiguration mqttConfiguration, RegistrarConfiguration registrarConfig, List<DeviceConfig> devices, List<ClimateDeviceConfig> climates)
    {
        _logger = logger;
        _mqttConfiguration = mqttConfiguration;
        _registrarConfig = registrarConfig;
        _devices = devices;
        _climates = climates;
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
            await RegisterDeviceAsync(client, device, stoppingToken);

        foreach (var climate in _climates)
            await RegisterClimateAsync(client, climate, stoppingToken);

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

    private async Task RegisterClimateAsync(IMqttClient client, ClimateDeviceConfig climate, CancellationToken ct)
    {
        var config = new
        {
            name = climate.Name,
            unique_id = climate.UniqueId,
            current_temperature_topic = climate.CurrentTemperatureTopic,
            current_temperature_template = climate.CurrentTemperatureTemplate,
            temperature_command_topic = climate.TemperatureCommandTopic,
            temperature_state_topic = climate.TemperatureStateTopic,
            temperature_state_template = climate.TemperatureStateTemplate,
            mode_state_topic = climate.ModeStateTopic,
            mode_state_template = climate.ModeStateTemplate,
            modes = climate.Modes,
            min_temp = climate.MinTemp,
            max_temp = climate.MaxTemp,
            temp_step = climate.TempStep,
            device = new
            {
                identifiers = new[] { climate.UniqueId },
                name = climate.Name,
                manufacturer = climate.Manufacturer,
                model = climate.Model
            }
        };

        await client.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic($"homeassistant/climate/{climate.UniqueId}/config")
                .WithPayload(JsonSerializer.Serialize(config))
                .WithRetainFlag()
                .Build(),
            ct);

        _logger.LogInformation("Registered climate {UniqueId}", climate.UniqueId);
    }
}
