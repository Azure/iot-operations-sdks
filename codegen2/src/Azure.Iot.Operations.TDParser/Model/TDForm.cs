namespace Azure.Iot.Operations.TDParser.Model
{
    using System.Text.Json.Serialization;

    public class TDForm
    {
        [JsonPropertyName("href")]
        public string? Href { get; set; }

        [JsonPropertyName("contentType")]
        public string? ContentType { get; set; }

        [JsonPropertyName("additionalResponses")]
        public TDSchemaReference[]? AdditionalResponses { get; set; }

        [JsonPropertyName("dtv:headerInfo")]
        public TDSchemaReference[]? HeaderInfo { get; set; }

        [JsonPropertyName("dtv:headerCode")]
        public string? HeaderCode { get; set; }

        [JsonPropertyName("dtv:serviceGroupId")]
        public string? ServiceGroupId { get; set; }

        [JsonPropertyName("dtv:topic")]
        public string? Topic { get; set; }

        [JsonPropertyName("op")]
        public TDStringArray? Op { get; set; }
    }
}
