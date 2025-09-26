namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class ContextSpecifier
    {
        public string? Remote { get; set; }

        public Dictionary<string, string>? Local { get; set; }

        public override string ToString()
        {
            if (Remote != null)
            {
                return Remote;
            }
            else if (Local != null)
            {
                return string.Join(", ", Local.Select(kv => $"{kv.Key}: {kv.Value}"));
            }
            else
            {
                return string.Empty;
            }
        }
    }

    public sealed class ContextSpecifierJsonConverter : JsonConverter<ContextSpecifier>
    {
        public override ContextSpecifier Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new ContextSpecifier
            {
                Remote = reader.TokenType == JsonTokenType.String ? reader.GetString() : null,
                Local = reader.TokenType == JsonTokenType.StartObject ? JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options) : null
            };
        }

        public override void Write(
            Utf8JsonWriter writer,
            ContextSpecifier amount,
            JsonSerializerOptions options)
        {
            if (amount.Remote != null)
            {
                writer.WriteStringValue(amount.Remote);
            }
            else if (amount.Local != null)
            {
                JsonSerializer.Serialize(writer, amount.Local, options);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
