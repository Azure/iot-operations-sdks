using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;
using System.Text;

internal class StringSerializer : IPayloadSerializer
{
    public SerializedPayloadContext ToBytes<T>(T? payload) where T : class
    {
        return new SerializedPayloadContext(null, "application/text", MqttPayloadFormatIndicator.CharacterData);
    }

    public T FromBytes<T>(
            byte[]? payload, 
            string? contentType, 
            MqttPayloadFormatIndicator payloadFormatIndicator) 
        where T : class
    {
        var content = Encoding.UTF8.GetString(payload);
        return content as T;
    }
}
