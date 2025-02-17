using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;
using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

internal class PubSubJsonUtf8Serializer : IPayloadSerializer
{

    private static readonly JsonSerializerOptions _exchangeJsonSerializerOptions = new JsonSerializerOptions {
        AllowTrailingCommas = true,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonReaderOptions _allowTrailingCommasInJsonReader = new JsonReaderOptions {
        AllowTrailingCommas = true,
    };

    public SerializedPayloadContext ToBytes<T>(T? payload) where T : class
    {
        var buffer = JsonSerializer.SerializeToUtf8Bytes<T>(payload, _exchangeJsonSerializerOptions);
        return new SerializedPayloadContext(buffer, "application/json", MqttPayloadFormatIndicator.CharacterData);
    }

    public T FromBytes<T>(
            byte[]? payload, 
            string? contentType, 
            MqttPayloadFormatIndicator payloadFormatIndicator) 
        where T : class
    {
        var utf8JsonReader = new Utf8JsonReader(payload, _allowTrailingCommasInJsonReader);
        var deserialized = JsonSerializer.Deserialize<T>(ref utf8JsonReader, _exchangeJsonSerializerOptions);
        Debug.Assert(deserialized != null, "error while deserialization");
        
        return deserialized;
    }
}
