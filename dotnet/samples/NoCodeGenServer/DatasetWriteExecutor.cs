using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;

[CommandTopic("{MqttCommandTopic}")]
internal class DatasetWriteExecutor : CommandExecutor<DatasetWriteRequest, DatasetWriteResponse>
{
    internal DatasetWriteExecutor(IMqttPubSubClient mqttClient)
        :base(mqttClient, "datasetWrite", new PubSubJsonUtf8Serializer())
    {
    }
}
