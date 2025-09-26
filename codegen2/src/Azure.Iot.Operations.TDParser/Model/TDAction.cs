namespace Azure.Iot.Operations.TDParser.Model
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class TDAction
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("input")]
        public TDDataSchema? Input { get; set; }

        [JsonPropertyName("output")]
        public TDDataSchema? Output { get; set; }

        [JsonPropertyName("idempotent")]
        public bool Idempotent { get; set; }

        [JsonPropertyName("safe")]
        public bool Safe { get; set; }

        [JsonPropertyName("forms")]
        public TDForm[]? Forms { get; set; }
    }
}
