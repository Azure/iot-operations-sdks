namespace Azure.Iot.Operations.TDParser.Model
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Serialization;

    public class TDDataSchema
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("const")]
        public int? Const { get; set; }

        [JsonPropertyName("minimum")]
        public double? Minimum { get; set; }

        [JsonPropertyName("maximum")]
        public double? Maximum { get; set; }

        [JsonPropertyName("format")]
        public string? Format { get; set; }

        [JsonPropertyName("pattern")]
        public string? Pattern { get; set; }

        [JsonPropertyName("contentEncoding")]
        public string? ContentEncoding { get; set; }

        [JsonPropertyName("dtv:additionalProperties")]
        public TDAdditionalPropSpecifier? AdditionalProperties { get; set; }

        [JsonPropertyName("enum")]
        public string[]? Enum { get; set; }

        [JsonPropertyName("required")]
        public string[]? Required { get; set; }

        [JsonPropertyName("dtv:errorMessage")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, TDDataSchema>? Properties { get; set; }

        [JsonPropertyName("items")]
        public TDDataSchema? Items { get; set; }

        public override int GetHashCode()
        {
            return (Title, Description, Type, Const, Minimum, Maximum, Format, Pattern, ContentEncoding, AdditionalProperties, Enum, Required, ErrorMessage, Properties, Items).GetHashCode();
        }

        public virtual bool Equals(TDDataSchema? other)
        {
            if (other == null)
            {
                return false;
            }

            return Title == other.Title &&
                Description == other.Description &&
                Type == other.Type &&
                Const == other.Const &&
                Minimum == other.Minimum &&
                Maximum == other.Maximum &&
                Format == other.Format &&
                Pattern == other.Pattern &&
                ContentEncoding == other.ContentEncoding &&
                ((AdditionalProperties == null && other.AdditionalProperties == null) || (AdditionalProperties?.Equals(other.AdditionalProperties) ?? false)) &&
                ((Enum == null && other.Enum == null) || (Enum != null && other.Enum != null && Enum.SequenceEqual(other.Enum))) &&
                ((Required == null && other.Required == null) || (Required != null && other.Required != null && Required.SequenceEqual(other.Required))) &&
                ErrorMessage == other.ErrorMessage &&
                Properties?.Count == other.Properties?.Count && (Properties == null || Properties.OrderBy(kv => kv.Key).SequenceEqual(other.Properties!.OrderBy(kv => kv.Key))) &&
                ((Items == null && other.Items == null) || (Items?.Equals(other.Items) ?? false));
        }
    }
}
