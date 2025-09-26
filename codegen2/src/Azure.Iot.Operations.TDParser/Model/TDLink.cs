namespace Azure.Iot.Operations.TDParser.Model
{
    using System.Text.Json.Serialization;

    public class TDLink
    {
        [JsonPropertyName("href")]
        public string? Href { get; set; }

        [JsonPropertyName("type")]
        public string? ContentType { get; set; }

        [JsonPropertyName("rel")]
        public string? Relation { get; set; }
    }
}
