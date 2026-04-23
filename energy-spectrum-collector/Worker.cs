using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Npgsql;

namespace energy_spectrum_collector;

internal sealed class Worker(
    ILogger<Worker> logger,
    HttpClient http,
    EnergySpectrumConfiguration cfg,
    DbConnectionString db) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan Buffer  = TimeSpan.FromMinutes(1);
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        http.DefaultRequestHeaders.Add("X-API-Key", cfg.ApiKey);

        var next = await GetNextStartAsync(ct);
        logger.LogInformation("Starting from {Next:O}", next);

        while (!ct.IsCancellationRequested)
        {
            var cutoff = DateTime.UtcNow - Buffer;

            if (next + Interval > cutoff)
            {
                var wait = (next + Interval + Buffer) - DateTime.UtcNow;
                if (wait > TimeSpan.Zero)
                {
                    logger.LogInformation("Caught up. Waiting {Wait:g} for next interval.", wait);
                    await Task.Delay(wait, ct);
                }
                continue;
            }

            var batch = BuildBatch(next, cutoff);

            try
            {
                var results = await FetchAsync(batch, ct);
                await WriteAsync(results, batch, ct);
                next = batch[^1].End;
                logger.LogInformation("Processed {Count} intervals up to {End:O}", batch.Count, next);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "API call failed, retrying in 30 s");
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
        }
    }

    private List<IntervalRequest> BuildBatch(DateTime from, DateTime cutoff)
    {
        var batch = new List<IntervalRequest>(BatchSize);
        var t = from;
        while (t + Interval <= cutoff && batch.Count < BatchSize)
        {
            batch.Add(new IntervalRequest(
                RequestId: (batch.Count + 1).ToString(),
                MpId: cfg.MpId,
                Start: t,
                End: t + Interval));
            t += Interval;
        }
        return batch;
    }

    private async Task<List<ApiResponse>> FetchAsync(List<IntervalRequest> batch, CancellationToken ct)
    {
        var payload = batch.Select(r => new
        {
            requestId = r.RequestId,
            mpId      = r.MpId,
            startDate = r.Start.ToString("O"),
            endDate   = r.End.ToString("O")
        });

        var response = await http.PostAsJsonAsync(cfg.BaseUrl, payload, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<ApiResponse>>(ct)
            ?? throw new InvalidOperationException("Empty response from API");
    }

    private async Task WriteAsync(List<ApiResponse> results, List<IntervalRequest> batch, CancellationToken ct)
    {
        var byRequestId = batch.ToDictionary(r => r.RequestId);

        await using var conn = new NpgsqlConnection(db.Value);
        await conn.OpenAsync(ct);
        await using var batchCmd = new NpgsqlBatch(conn);

        foreach (var result in results)
        {
            if (!byRequestId.TryGetValue(result.RequestId, out var req)) continue;

            void AddRow(string metric, double? value)
            {
                if (value is null) return;
                var cmd = batchCmd.CreateBatchCommand();
                cmd.CommandText =
                    "INSERT INTO analog_readings (time, device_type, device_id, metric_name, value) " +
                    "VALUES ($1, 'power_meter', $2, $3, $4) ON CONFLICT DO NOTHING";
                cmd.Parameters.Add(new NpgsqlParameter { Value = req.Start });
                cmd.Parameters.Add(new NpgsqlParameter { Value = cfg.MpId });
                cmd.Parameters.Add(new NpgsqlParameter { Value = metric });
                cmd.Parameters.Add(new NpgsqlParameter { Value = value.Value });
                batchCmd.BatchCommands.Add(cmd);
            }

            AddRow("active_energy",        result.ActiveEnergy);
            AddRow("average_active_power", result.AverageActivePower);
        }

        if (batchCmd.BatchCommands.Count > 0)
            await batchCmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<DateTime> GetNextStartAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(db.Value);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT MAX(time) FROM analog_readings WHERE device_type = 'power_meter' AND device_id = $1", conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = cfg.MpId });

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is DateTime last)
            return last + Interval;

        return DateTime.SpecifyKind(cfg.StartDate, DateTimeKind.Utc);
    }

    private sealed record IntervalRequest(string RequestId, string MpId, DateTime Start, DateTime End);

    private sealed class ApiResponse
    {
        [JsonPropertyName("RequestId")]          public string  RequestId          { get; init; } = "";
        [JsonPropertyName("ActiveEnergy")]        public double? ActiveEnergy       { get; init; }
        [JsonPropertyName("AverageActivePower")]  public double? AverageActivePower { get; init; }
    }
}
