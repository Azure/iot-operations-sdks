namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;
    using MQTTnet.Client;

    public class TestCommandInvoker : CommandInvoker<string, string>
    {
        internal TestCommandInvoker(IMqttPubSubClient mqttClient, string commandName)
            : base(mqttClient, commandName, new Utf8JsonSerializer())
        {
        }
    }
}
