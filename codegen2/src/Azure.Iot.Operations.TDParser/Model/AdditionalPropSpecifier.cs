namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class AdditionalPropSpecifier
    {
        public bool? Boolean { get; set; }

        public DataSchema? DataSchema { get; set; }

        public override string ToString()
        {
            if (Boolean.HasValue)
            {
                return Boolean.Value.ToString();
            }
            else if (DataSchema != null)
            {
                return DataSchema.Type ?? string.Empty;
            }
            else
            {
                return string.Empty;
            }
        }
    }

    public sealed class AdditionalPropSpecifierJsonConverter : JsonConverter<AdditionalPropSpecifier>
    {
        public override AdditionalPropSpecifier Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new AdditionalPropSpecifier
            {
                Boolean = reader.TokenType == JsonTokenType.True ? true : reader.TokenType == JsonTokenType.False ? false : (bool?)null,
                DataSchema = reader.TokenType == JsonTokenType.StartObject ? JsonSerializer.Deserialize<DataSchema>(ref reader, options) : null
            };
        }

        public override void Write(
            Utf8JsonWriter writer,
            AdditionalPropSpecifier amount,
            JsonSerializerOptions options)
        {
            if (amount.Boolean.HasValue)
            {
                writer.WriteBooleanValue(amount.Boolean.Value);
            }
            else if (amount.DataSchema != null)
            {
                JsonSerializer.Serialize(writer, amount.DataSchema, options);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
