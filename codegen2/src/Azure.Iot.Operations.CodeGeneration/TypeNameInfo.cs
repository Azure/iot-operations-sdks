namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class TypeNameInfo
    {
        [JsonPropertyName("suppressTitles")]
        public bool SuppressTitles { get; set; }

        [JsonPropertyName("nameRules")]
        public Dictionary<string, string>? NameRules { get; set; }

        [JsonPropertyName("capitalizeCaptures")]
        public bool CapitalizeCaptures { get; set; }
    }
}
