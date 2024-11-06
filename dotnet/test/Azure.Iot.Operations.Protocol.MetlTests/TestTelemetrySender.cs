namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Telemetry;
    using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;
    using MQTTnet.Client;

    public class TestTelemetrySender : TelemetrySender<string>
    {
        internal TestTelemetrySender(IMqttPubSubClient mqttClient, string? telemetryName)
            : base(mqttClient, telemetryName, new Utf8JsonSerializer())
        {
        }
    }
}
