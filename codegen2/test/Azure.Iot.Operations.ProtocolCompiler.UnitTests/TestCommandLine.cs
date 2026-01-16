namespace Azure.Iot.Operations.ProtocolCompiler.UnitTests
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class TestCommandLine
    {
        [JsonPropertyName("thingFiles")]
        public string[] ThingFiles { get; set; } = [];

        [JsonPropertyName("schemas")]
        public string[] SchemaFiles { get; set; } = [];

        [JsonPropertyName("typeNamer")]
        public string? TypeNamerFile { get; set; }

        [JsonPropertyName("outDir")]
        public string? OutputDir { get; set; }

        [JsonPropertyName("workingDir")]
        public string? WorkingDir { get; set; }

        [JsonPropertyName("namespaace")]
        public string? GenNamespace { get; set; }

        [JsonPropertyName("sdkPath")]
        public string? SdkPath { get; set; }

        [JsonPropertyName("lang")]
        public string? Language { get; set; }

        [JsonPropertyName("clientOnly")]
        public bool ClientOnly { get; set; }

        [JsonPropertyName("serverOnly")]
        public bool ServerOnly { get; set; }

        [JsonPropertyName("noProj")]
        public bool NoProj { get; set; }

        [JsonPropertyName("defaultImpl")]
        public bool DefaultImpl { get; set; }
    }
}
