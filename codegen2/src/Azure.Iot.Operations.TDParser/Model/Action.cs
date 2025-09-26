namespace Azure.Iot.Operations.TDParser.Model
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class Action
    {
        [JsonPropertyName("descriptions")]
        public Dictionary<string, string>? Descriptions { get; set; }

        [JsonPropertyName("input")]
        public DataSchema? Input { get; set; }

        [JsonPropertyName("output")]
        public DataSchema? Output { get; set; }

        [JsonPropertyName("idempotent")]
        public bool Idempotent { get; set; }

        [JsonPropertyName("safe")]
        public bool Safe { get; set; }

        [JsonPropertyName("forms")]
        public Form[]? Forms { get; set; }
    }
}
