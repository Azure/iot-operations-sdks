namespace DtdlMcp
{
    using System.Text.Json.Serialization;

    public class ModelInfo
    {
        [JsonPropertyName("modelId")]
        public string ModelId { get; set; } = string.Empty;

        [JsonPropertyName("specVersion")]
        public string SpecVersion { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("typeRef")]
        public string TypeRef { get; set; } = string.Empty;
    }
}
