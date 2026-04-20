using ha_device_registrar;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((c, s) =>
    {
        s.AddSingleton(new MqttConfiguration(
            Host: c.Configuration["Mqtt:Host"]!,
            Port: ushort.Parse(c.Configuration["Mqtt:Port"]!)));

        var devices = c.Configuration.GetSection("Devices").Get<List<DeviceConfig>>() ?? [];
        s.AddSingleton(devices);

        s.AddHostedService<Worker>();
    })
    .Build()
    .Run();
