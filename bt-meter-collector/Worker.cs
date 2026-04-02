using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private XiaomiLywsd03MmcListener? _listener;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listener = new XiaomiLywsd03MmcListener(
            async (mac, ct) =>
            {
                _logger.LogInformation("Device appeared: {Mac}", mac);
                await Task.CompletedTask;
            },
            async (sample, ct) =>
            {
                _logger.LogInformation(
                    "Sample mac={Mac} rssi={Rssi} temp={Temp} hum={Hum} battMv={BattMv} battPct={BattPct}",
                    sample.Mac, sample.Rssi, sample.Temperature, sample.Humidity, sample.BattMv, sample.BattPct);
                await Task.CompletedTask;
            });

        await _listener.StartListeningAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_listener is not null)
            await _listener.DisposeAsync();

        await base.StopAsync(cancellationToken);
    }
}