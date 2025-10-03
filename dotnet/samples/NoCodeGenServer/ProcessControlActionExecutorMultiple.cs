
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;

[CommandTopic("{MqttCommandTopic}/{Asset}/{ProcessControlGroup}/{Action}")]
internal class ProcessControlActionExecutorMultiple : CommandExecutor<string, string>
{
    internal ProcessControlActionExecutorMultiple(IMqttPubSubClient mqttClient)
        :base(mqttClient, "process-control-action-multiple", new StringSerializer())
    {
    }
}
