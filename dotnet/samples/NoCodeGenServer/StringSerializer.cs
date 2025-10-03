using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;
using System.Text;

internal class StringSerializer : IPayloadSerializer
{
    public SerializedPayloadContext ToBytes<T>(T? payload) where T : class
    {
        var buffer = Encoding.UTF8.GetBytes((payload as string) ?? string.Empty);
        return new SerializedPayloadContext(buffer, "application/text", MqttPayloadFormatIndicator.CharacterData);
    }

    public T FromBytes<T>(
            byte[]? payload, 
            string? contentType, 
            MqttPayloadFormatIndicator payloadFormatIndicator) 
        where T : class
    {
        var content = payload != null ? Encoding.UTF8.GetString(payload) : string.Empty;
        return content as T;
    }
}
