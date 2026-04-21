namespace hitachi_yutaki_2016_mqtt_gateway;

internal enum RegisterKind { Analog, Enum, Bitmask, Raw }

internal enum RegisterGroup { Unit, Circuit1, Circuit2, Dhw, Pool, Diagnostics }

internal sealed class RegisterDefinition
{
    public required ushort Address { get; init; }
    public required string Name { get; init; }
    public required RegisterKind Kind { get; init; }
    public required RegisterGroup Group { get; init; }
    public bool IsWritable { get; init; }
    public bool IsSigned { get; init; }
    public double Scale { get; init; } = 1.0;
    public string? Unit { get; init; }
    public string? BitmaskPrefix { get; init; }
    public IReadOnlyDictionary<int, string>? EnumValues { get; init; }
    public IReadOnlyDictionary<int, string>? BitNames { get; init; }
}
