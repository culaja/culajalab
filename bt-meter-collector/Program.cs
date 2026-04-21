using bt_meter_collector;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((c, s) =>
    {
        var mqttHost = c.Configuration["Mqtt:Host"] ?? throw new InvalidOperationException("Mqtt:Host is not configured in appsettings.json.");
        s.AddSingleton(new MqttConfiguration(
            Host: mqttHost,
            Port: ushort.Parse(c.Configuration["Mqtt:Port"]!)));
        s.AddSingleton(new BluetoothConfiguration(
            HciDevice: ushort.Parse(c.Configuration["Bluetooth:HciDevice"] ?? "0")));
        s.AddHostedService<Worker>();
    })
    .Build()
    .Run();