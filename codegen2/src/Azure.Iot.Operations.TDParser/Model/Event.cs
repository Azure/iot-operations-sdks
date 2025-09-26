namespace Azure.Iot.Operations.TDParser.Model
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class Event
    {
        [JsonPropertyName("descriptions")]
        public Dictionary<string, string>? Descriptions { get; set; }

        [JsonPropertyName("data")]
        public DataSchema? Data { get; set; }

        [JsonPropertyName("forms")]
        public Form[]? Forms { get; set; }
    }
}
