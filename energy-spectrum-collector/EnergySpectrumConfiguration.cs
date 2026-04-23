namespace energy_spectrum_collector;

internal sealed record EnergySpectrumConfiguration
{
    public required string BaseUrl { get; init; }
    public required string ApiKey { get; init; }
    public required string MpId { get; init; }
    public required DateTime StartDate { get; init; }
}

internal sealed record DbConnectionString(string Value);
