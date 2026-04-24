using System.Text.Json;
using MQTTnet;
using Npgsql;

namespace mqtt_timescale_consumer;

internal sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly MqttConfiguration _mqttConfig;
    private readonly ConsumerConfiguration _consumerConfig;
    private readonly string _connectionString;

    public Worker(ILogger<Worker> logger, MqttConfiguration mqttConfig, ConsumerConfiguration consumerConfig, string connectionString)
    {
        _logger = logger;
        _mqttConfig = mqttConfig;
        _consumerConfig = consumerConfig;
        _connectionString = connectionString;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await InitializeSchemaAsync(stoppingToken);

        var client = new MqttClientFactory().CreateMqttClient();

        client.ApplicationMessageReceivedAsync += args =>
            HandleMessageAsync(args.ApplicationMessage.Topic,
                args.ApplicationMessage.ConvertPayloadToString() ?? string.Empty,
                stoppingToken);

        await client.ConnectAsync(
            new MqttClientOptionsBuilder()
                .WithTcpServer(_mqttConfig.Host, _mqttConfig.Port)
                .Build(),
            stoppingToken);

        await client.SubscribeAsync(
            new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter($"{_consumerConfig.HitachiTopicPrefix}/+")
                .WithTopicFilter($"{_consumerConfig.SensorTopicPrefix}/+/state")
                .Build(),
            stoppingToken);

        _logger.LogInformation("Subscribed to MQTT topics, consuming...");

        await Task.Delay(Timeout.Infinite, stoppingToken);

        await client.DisconnectAsync(cancellationToken: CancellationToken.None);
    }

    private async Task HandleMessageAsync(string topic, string payload, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(payload)) return;

        try
        {
            if (topic.StartsWith(_consumerConfig.HitachiTopicPrefix))
                await HandleHitachiMessageAsync(payload, ct);
            else if (topic.StartsWith(_consumerConfig.SensorTopicPrefix))
                await HandleSensorMessageAsync(topic, payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message on topic {Topic}", topic);
        }
    }

    private async Task HandleHitachiMessageAsync(string payload, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(payload);
        var deviceId = _consumerConfig.HitachiDeviceId;
        var now = doc.RootElement.TryGetProperty("timestamp", out var ts)
            ? DateTime.Parse(ts.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind)
            : DateTime.UtcNow;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var batch = new NpgsqlBatch(conn);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Number)
            {
                var cmd = batch.CreateBatchCommand();
                cmd.CommandText = "INSERT INTO analog_readings (time, device_type, device_id, metric_name, value) VALUES ($1, 'heat_pump', $2, $3, $4) ON CONFLICT DO NOTHING";
                cmd.Parameters.Add(new NpgsqlParameter { Value = now });
                cmd.Parameters.Add(new NpgsqlParameter { Value = deviceId });
                cmd.Parameters.Add(new NpgsqlParameter { Value = prop.Name });
                cmd.Parameters.Add(new NpgsqlParameter { Value = prop.Value.GetDouble() });
                batch.BatchCommands.Add(cmd);
            }
            else if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var cmd = batch.CreateBatchCommand();
                cmd.CommandText = "INSERT INTO status_readings (time, device_type, device_id, metric_name, value) VALUES ($1, 'heat_pump', $2, $3, $4) ON CONFLICT DO NOTHING";
                cmd.Parameters.Add(new NpgsqlParameter { Value = now });
                cmd.Parameters.Add(new NpgsqlParameter { Value = deviceId });
                cmd.Parameters.Add(new NpgsqlParameter { Value = prop.Name });
                cmd.Parameters.Add(new NpgsqlParameter { Value = prop.Value.GetString()! });
                batch.BatchCommands.Add(cmd);
            }
            else if (prop.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                var cmd = batch.CreateBatchCommand();
                cmd.CommandText = "INSERT INTO analog_readings (time, device_type, device_id, metric_name, value) VALUES ($1, 'heat_pump', $2, $3, $4) ON CONFLICT DO NOTHING";
                cmd.Parameters.Add(new NpgsqlParameter { Value = now });
                cmd.Parameters.Add(new NpgsqlParameter { Value = deviceId });
                cmd.Parameters.Add(new NpgsqlParameter { Value = prop.Name });
                cmd.Parameters.Add(new NpgsqlParameter { Value = prop.Value.GetBoolean() ? 1.0 : 0.0 });
                batch.BatchCommands.Add(cmd);
            }
        }

        if (batch.BatchCommands.Count > 0)
        {
            await batch.ExecuteNonQueryAsync(ct);
            _logger.LogInformation("Inserted {Count} rows into analog_readings/status_readings for device {DeviceId}", batch.BatchCommands.Count, deviceId);
        }
    }

    private async Task HandleSensorMessageAsync(string topic, string payload, CancellationToken ct)
    {
        // topic: rumenka/sensor/{mac}/state
        var parts = topic.Split('/');
        if (parts.Length < 4) return;
        var mac = parts[^2];

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var now = root.TryGetProperty("timestamp", out var ts)
            ? DateTime.Parse(ts.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind)
            : DateTime.UtcNow;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var batch = new NpgsqlBatch(conn);

        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Number) continue;

            var cmd = batch.CreateBatchCommand();
            cmd.CommandText = "INSERT INTO analog_readings (time, device_type, device_id, metric_name, value) VALUES ($1, 'thermometer', $2, $3, $4) ON CONFLICT DO NOTHING";
            cmd.Parameters.Add(new NpgsqlParameter { Value = now });
            cmd.Parameters.Add(new NpgsqlParameter { Value = mac });
            cmd.Parameters.Add(new NpgsqlParameter { Value = prop.Name });
            cmd.Parameters.Add(new NpgsqlParameter { Value = prop.Value.GetDouble() });
            batch.BatchCommands.Add(cmd);
        }

        if (batch.BatchCommands.Count > 0)
        {
            await batch.ExecuteNonQueryAsync(ct);
            _logger.LogInformation("Inserted {Count} rows into analog_readings for device {DeviceId}", batch.BatchCommands.Count, mac);
        }
    }

    private async Task InitializeSchemaAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS analog_readings (
                time        TIMESTAMPTZ      NOT NULL,
                device_type TEXT             NOT NULL,
                device_id   TEXT             NOT NULL,
                metric_name TEXT             NOT NULL,
                value       DOUBLE PRECISION NOT NULL
            );

            CREATE TABLE IF NOT EXISTS status_readings (
                time        TIMESTAMPTZ NOT NULL,
                device_type TEXT        NOT NULL,
                device_id   TEXT        NOT NULL,
                metric_name TEXT        NOT NULL,
                value       TEXT        NOT NULL
            );

            SELECT create_hypertable('analog_readings', 'time',
                chunk_time_interval => INTERVAL '1 day', if_not_exists => TRUE);
            SELECT create_hypertable('status_readings', 'time',
                chunk_time_interval => INTERVAL '1 day', if_not_exists => TRUE);

            CREATE UNIQUE INDEX IF NOT EXISTS analog_readings_unique ON analog_readings (time, device_id, metric_name);
            CREATE UNIQUE INDEX IF NOT EXISTS status_readings_unique ON status_readings (time, device_id, metric_name);

            CREATE INDEX IF NOT EXISTS analog_readings_lookup ON analog_readings (device_id, metric_name, time DESC);
            CREATE INDEX IF NOT EXISTS status_readings_lookup ON status_readings (device_id, metric_name, time DESC);

            ALTER TABLE analog_readings SET (
                timescaledb.compress,
                timescaledb.compress_segmentby = 'device_id, metric_name',
                timescaledb.compress_orderby   = 'time DESC'
            );
            ALTER TABLE status_readings SET (
                timescaledb.compress,
                timescaledb.compress_segmentby = 'device_id, metric_name',
                timescaledb.compress_orderby   = 'time DESC'
            );

            SELECT add_compression_policy('analog_readings', INTERVAL '30 days', if_not_exists => TRUE);
            SELECT add_compression_policy('status_readings', INTERVAL '30 days', if_not_exists => TRUE);

            CREATE MATERIALIZED VIEW IF NOT EXISTS analog_hourly
            WITH (timescaledb.continuous) AS
            SELECT
                time_bucket('1h', time) AS bucket,
                device_id,
                metric_name,
                first(value, time) AS first_val,
                last(value, time)  AS last_val,
                min(value)         AS min_val,
                max(value)         AS max_val,
                avg(value)         AS avg_val
            FROM analog_readings
            GROUP BY 1, 2, 3
            WITH NO DATA;

            SELECT add_continuous_aggregate_policy('analog_hourly',
                start_offset      => INTERVAL '3h',
                end_offset        => INTERVAL '1h',
                schedule_interval => INTERVAL '1h',
                if_not_exists     => TRUE);

            CREATE MATERIALIZED VIEW IF NOT EXISTS analog_daily
            WITH (timescaledb.continuous) AS
            SELECT
                time_bucket('1 day', time, 'Europe/Belgrade') AS bucket,
                device_id,
                metric_name,
                first(value, time) AS first_val,
                last(value, time)  AS last_val,
                min(value)         AS min_val,
                max(value)         AS max_val,
                avg(value)         AS avg_val
            FROM analog_readings
            GROUP BY 1, 2, 3
            WITH NO DATA;

            SELECT add_continuous_aggregate_policy('analog_daily',
                start_offset      => INTERVAL '3 days',
                end_offset        => INTERVAL '1 day',
                schedule_interval => INTERVAL '1 day',
                if_not_exists     => TRUE);
            """, conn);

        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogInformation("Schema initialized.");
    }
}
