using hitachi_yutaki_2016_mqtt_gateway;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((c, s) =>
    {
        var modbusHost = c.Configuration["Modbus:Host"] ?? throw new InvalidOperationException("Modbus:Host is not configured in appsettings.json.");
        s.AddSingleton(new ModbusConfiguration(
            Host: modbusHost,
            Port: int.Parse(c.Configuration["Modbus:Port"] ?? "502"),
            UnitId: byte.Parse(c.Configuration["Modbus:UnitId"] ?? "1")));

        var mqttHost = c.Configuration["Mqtt:Host"] ?? throw new InvalidOperationException("Mqtt:Host is not configured in appsettings.json.");
        s.AddSingleton(new MqttConfiguration(
            Host: mqttHost,
            Port: ushort.Parse(c.Configuration["Mqtt:Port"] ?? "1883")));

        s.AddSingleton(new GatewayConfiguration(
            ForcePublishInterval: TimeSpan.FromSeconds(double.Parse(c.Configuration["Gateway:ForcePublishIntervalSeconds"] ?? "60"))));

        s.AddHostedService<Worker>();
    })
    .Build()
    .Run();
