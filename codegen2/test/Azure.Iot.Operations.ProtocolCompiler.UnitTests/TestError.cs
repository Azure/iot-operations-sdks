namespace Azure.Iot.Operations.ProtocolCompiler.UnitTests
{
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.CodeGeneration;

    public class TestError
    {
        [JsonPropertyName("condition")]
        public string Condition { get; set; } = string.Empty;

        [JsonPropertyName("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonPropertyName("line")]
        public int LineNumber { get; set; } = 0;

        [JsonPropertyName("cfLine")]
        public int CfLineNumber { get; set; } = 0;

        [JsonPropertyName("crossRef")]
        public string CrossRef { get; set; } = string.Empty;
    }
}
