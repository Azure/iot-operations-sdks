namespace Azure.Iot.Operations.TDParser.Model
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class DataSchema
    {
        [JsonPropertyName("descriptions")]
        public Dictionary<string, string>? Descriptions { get; set; }

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
        public AdditionalPropSpecifier? AdditionalProperties { get; set; }

        [JsonPropertyName("enum")]
        public string[]? Enum { get; set; }

        [JsonPropertyName("required")]
        public string[]? Required { get; set; }

        [JsonPropertyName("dtv:errorMessage")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, DataSchema>? Properties { get; set; }

        [JsonPropertyName("items")]
        public DataSchema? Items { get; set; }
    }
}
