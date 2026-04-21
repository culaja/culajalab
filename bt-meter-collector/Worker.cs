namespace bt_meter_collector;

internal sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly XiaomiLywsd03MmcRawListener _listener;
    private readonly MqttPublisher _mqttPublisher;

    public Worker(ILogger<Worker> logger, MqttConfiguration mqttConfiguration)
    {
        _logger = logger;
        _listener = new XiaomiLywsd03MmcRawListener(SampleReceived);
        _mqttPublisher = new MqttPublisher(logger, mqttConfiguration);
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