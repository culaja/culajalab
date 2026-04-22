namespace mqtt_timescale_consumer;

internal sealed record MqttConfiguration(string Host, ushort Port);
internal sealed record ConsumerConfiguration(string HitachiTopicPrefix, string HitachiDeviceId, string SensorTopicPrefix);
