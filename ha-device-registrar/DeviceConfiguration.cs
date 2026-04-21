namespace ha_device_registrar;

public sealed record MqttConfiguration(string Host, ushort Port);
public sealed record RegistrarConfiguration(int SensorExpirySeconds);

public sealed record DeviceConfig
{
    public string Mac { get; init; } = "";
    public string Model { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string StateTopic { get; init; } = "";
    public List<SensorConfig> Sensors { get; init; } = [];
}

public sealed record SensorConfig
{
    public string Name { get; init; } = "";
    public string DeviceClass { get; init; } = "";
    public string UnitOfMeasurement { get; init; } = "";
    public string ValueTemplate { get; init; } = "";
    public string StateClass { get; init; } = "";
}

public sealed record ClimateDeviceConfig
{
    public string Name { get; init; } = "";
    public string UniqueId { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string Model { get; init; } = "";
    public string CurrentTemperatureTopic { get; init; } = "";
    public string CurrentTemperatureTemplate { get; init; } = "";
    public string TemperatureCommandTopic { get; init; } = "";
    public string TemperatureStateTopic { get; init; } = "";
    public string TemperatureStateTemplate { get; init; } = "";
    public string ModeCommandTopic { get; init; } = "";
    public string ModeCommandTemplate { get; init; } = "";
    public string ModeStateTopic { get; init; } = "";
    public string ModeStateTemplate { get; init; } = "";
    public List<string> Modes { get; init; } = [];
    public double MinTemp { get; init; } = 16;
    public double MaxTemp { get; init; } = 30;
    public double TempStep { get; init; } = 0.5;
}
