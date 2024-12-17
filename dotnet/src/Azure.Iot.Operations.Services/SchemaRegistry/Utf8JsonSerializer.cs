namespace Azure.Iot.Operations.Services.SchemaRegistry
{
    using System.Runtime.Serialization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Protocol;

    public class Utf8JsonSerializer : IPayloadSerializer
    {
        protected static readonly JsonSerializerOptions jsonSerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new DurationJsonConverter(),
                new DateJsonConverter(),
                new TimeJsonConverter(),
            }
        };

        public string ContentType => "application/json";

        public int CharacterDataFormatIndicator => 1;

        public T FromBytes<T>(byte[]? payload)
            where T : class
        {
            if (payload == null || payload.Length == 0)
            {
                if (typeof(T) != typeof(EmptyJson))
                {
                    throw new SerializationException();
                }

                return (new EmptyJson() as T)!;
            }

            Utf8JsonReader reader = new(payload);
            return JsonSerializer.Deserialize<T>(ref reader, jsonSerializerOptions)!;
        }

        public byte[]? ToBytes<T>(T? payload)
            where T : class
        {
            if (typeof(T) == typeof(EmptyJson))
            {
                return null;
            }

            return JsonSerializer.SerializeToUtf8Bytes(payload, jsonSerializerOptions);
        }
    }
}
