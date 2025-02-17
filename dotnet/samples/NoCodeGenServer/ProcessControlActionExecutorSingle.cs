
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;

[CommandTopic("{MqttCommandTopic}/{Asset}/{ProcessControlGroup}/{Action}")]
internal class ProcessControlActionExecutorSingle : CommandExecutor<string, string>
{
    internal ProcessControlActionExecutorSingle(IMqttPubSubClient mqttClient)
        :base(mqttClient, "process-control-action-single", new PubSubJsonUtf8Serializer())
    {
    }
}
