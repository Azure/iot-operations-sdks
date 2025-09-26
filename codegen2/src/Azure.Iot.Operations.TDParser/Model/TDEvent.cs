namespace Azure.Iot.Operations.TDParser.Model
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class TDEvent
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("data")]
        public TDDataSchema? Data { get; set; }

        [JsonPropertyName("dtv:placeholder")]
        public bool Placeholder { get; set; }

        [JsonPropertyName("forms")]
        public TDForm[]? Forms { get; set; }
    }
}
