namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class TDStringArray
    {
        public List<string> Values { get; set; } = new();

        public override string ToString()
        {
            return string.Join(", ", Values.Select(s => $"{s}"));
        }
    }

    public sealed class TDStringArrayJsonConverter : JsonConverter<TDStringArray>
    {
        public override TDStringArray Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                return new TDStringArray
                {
                    Values = new List<string> { reader.GetString()! }
                };
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                return new TDStringArray
                {
                    Values = JsonSerializer.Deserialize<List<string>>(ref reader, options)!
                };
            }

            return new TDStringArray();
        }

        public override void Write(
            Utf8JsonWriter writer,
            TDStringArray amount,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, amount.Values, options);
        }
    }
}
