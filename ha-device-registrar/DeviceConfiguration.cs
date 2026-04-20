namespace ha_device_registrar;

public sealed record MqttConfiguration(string Host, ushort Port);

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
