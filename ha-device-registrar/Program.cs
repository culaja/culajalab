using ha_device_registrar;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((c, s) =>
    {
        s.AddSingleton(new MqttConfiguration(
            Host: c.Configuration["Mqtt:Host"]!,
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
