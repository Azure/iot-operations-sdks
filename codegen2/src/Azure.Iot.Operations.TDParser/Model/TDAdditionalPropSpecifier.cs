namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class TDAdditionalPropSpecifier
    {
        public bool? Boolean { get; set; }

        public TDDataSchema? DataSchema { get; set; }

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

        public virtual bool Equals(TDAdditionalPropSpecifier? other)
        {
            if (other == null)
            {
                return false;
            }

            return Boolean == other.Boolean &&
                ((DataSchema == null && other.DataSchema == null) || (DataSchema?.Equals(other.DataSchema) ?? false));
        }
    }

    public sealed class AdditionalPropSpecifierJsonConverter : JsonConverter<TDAdditionalPropSpecifier>
    {
        public override TDAdditionalPropSpecifier Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new TDAdditionalPropSpecifier
            {
                Boolean = reader.TokenType == JsonTokenType.True ? true : reader.TokenType == JsonTokenType.False ? false : (bool?)null,
                DataSchema = reader.TokenType == JsonTokenType.StartObject ? JsonSerializer.Deserialize<TDDataSchema>(ref reader, options) : null
            };
        }

        public override void Write(
            Utf8JsonWriter writer,
            TDAdditionalPropSpecifier amount,
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
