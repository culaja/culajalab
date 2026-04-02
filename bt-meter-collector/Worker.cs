namespace bt_meter_collector;

internal sealed record MqttConfiguration(string Host, int Port);

internal sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly XiaomiLywsd03MmcListener _listener;
    private readonly HomeAssistantSensorMqttPublisher _mqttPublisher;

    public Worker(ILogger<Worker> logger, MqttConfiguration mqttConfiguration)
    {
        _logger = logger;
        _listener = new XiaomiLywsd03MmcListener(DeviceAppeared, SampleReceived);
        _mqttPublisher = new HomeAssistantSensorMqttPublisher(logger, mqttConfiguration);
    }

    private Task DeviceAppeared(string mac, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Device appeared: {mac}");
        return _mqttPublisher.RegisterSensorAsync(mac, cancellationToken);
    }

    private Task SampleReceived(Sample sample, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sample received: {@Sample}", sample);
        return _mqttPublisher.PublishSampleAsync(sample, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listener.StartListening(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}