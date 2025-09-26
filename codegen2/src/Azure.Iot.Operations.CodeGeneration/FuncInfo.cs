namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Text.Json.Serialization;

    public class FuncInfo
    {
        [JsonPropertyName("in")]
        public string[]? Input { get; set; }

        [JsonPropertyName("out")]
        public string? Output { get; set; }

        [JsonPropertyName("capitalize")]
        public bool Capitalize { get; set; }
    }
}
