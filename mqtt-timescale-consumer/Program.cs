using mqtt_timescale_consumer;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((c, s) =>
    {
        var mqttHost = c.Configuration["Mqtt:Host"] ?? throw new InvalidOperationException("Mqtt:Host is not configured.");
        s.AddSingleton(new MqttConfiguration(
            Host: mqttHost,
            Port: ushort.Parse(c.Configuration["Mqtt:Port"] ?? "1883")));

        var connectionString = c.Configuration.GetConnectionString("TimescaleDb")
            ?? throw new InvalidOperationException("ConnectionStrings:TimescaleDb is not configured.");
        s.AddSingleton(connectionString);

        s.AddSingleton(new ConsumerConfiguration(
            HitachiTopicPrefix: c.Configuration["Consumer:HitachiTopicPrefix"] ?? "rumenka/hitachi/yutaki2016",
            HitachiDeviceId: c.Configuration["Consumer:HitachiDeviceId"] ?? "yutaki2016",
            SensorTopicPrefix: c.Configuration["Consumer:SensorTopicPrefix"] ?? "rumenka/sensor"));

        s.AddHostedService<Worker>();
    })
    .Build()
    .Run();
