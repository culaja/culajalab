using ha_device_registrar;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((c, s) =>
    {
        var mqttHost = c.Configuration["Mqtt:Host"] ?? throw new InvalidOperationException("Mqtt:Host is not configured in appsettings.json.");
        s.AddSingleton(new MqttConfiguration(
            Host: mqttHost,
            Port: ushort.Parse(c.Configuration["Mqtt:Port"]!)));

        var devices = c.Configuration.GetSection("Devices").Get<List<DeviceConfig>>() ?? [];
        var invalid = devices.Where(d => string.IsNullOrWhiteSpace(d.Mac)).ToList();
        if (invalid.Count > 0)
            throw new InvalidOperationException($"Device at index {devices.IndexOf(invalid[0])} has no MAC address configured in appsettings.json.");
        s.AddSingleton(devices);

        s.AddHostedService<Worker>();
    })
    .Build()
    .Run();
