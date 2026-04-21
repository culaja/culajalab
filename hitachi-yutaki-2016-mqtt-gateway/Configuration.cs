namespace hitachi_yutaki_2016_mqtt_gateway;

internal sealed record ModbusConfiguration(string Host, int Port, byte UnitId);
internal sealed record MqttConfiguration(string Host, ushort Port);
internal sealed record GatewayConfiguration(TimeSpan ForcePublishInterval);
