using bt_meter_collector;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((c, s) =>
    {
        s.AddSingleton(new MqttConfiguration(
            Host: c.Configuration["Mqtt:Host"]!,
            Port: ushort.Parse(c.Configuration["Mqtt:Port"]!)));
        s.AddHostedService<Worker>();
    })
    .Build()
    .Run();