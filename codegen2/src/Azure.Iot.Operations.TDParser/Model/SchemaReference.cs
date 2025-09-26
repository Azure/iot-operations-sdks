namespace Azure.Iot.Operations.TDParser.Model
{
    using System.Text.Json.Serialization;

    public class SchemaReference
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("contentType")]
        public string? ContentType { get; set; }

        [JsonPropertyName("schema")]
        public string? Schema { get; set; }
    }
}
