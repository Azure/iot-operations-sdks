namespace DtdlMcp
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class SpecInfo
    {
        [JsonPropertyName("file")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("dateTime")]
        public string ConversionTime { get; set; } = string.Empty;

        [JsonPropertyName("toolVersion")]
        public string ToolVersion { get; set; } = string.Empty;

        [JsonPropertyName("ontology")]
        public string Ontology { get; set; } = string.Empty;

        [JsonPropertyName("events")]
        public List<ModelInfo> Events { get; set; } = new ();

        [JsonPropertyName("composites")]
        public List<ModelInfo> Composites { get; set; } = new();

        [JsonPropertyName("otherTypes")]
        public List<ModelInfo> OtherTypes { get; set; } = new();
    }
}
