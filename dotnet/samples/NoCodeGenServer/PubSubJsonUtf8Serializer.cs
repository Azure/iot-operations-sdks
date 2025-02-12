using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;

internal class PubSubJsonUtf8Serializer : IPayloadSerializer
{
    public SerializedPayloadContext ToBytes<T>(T? payload) where T : class
    {
        return new SerializedPayloadContext(null, null, MqttPayloadFormatIndicator.CharacterData);
    }

    public T FromBytes<T>(
            byte[]? payload, 
            string? contentType, 
            MqttPayloadFormatIndicator payloadFormatIndicator) 
        where T : class
    {
        return default(T);
    }
}
