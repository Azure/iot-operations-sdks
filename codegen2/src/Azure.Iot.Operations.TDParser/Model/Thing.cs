namespace Azure.Iot.Operations.TDParser.Model
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class Thing
    {
        [JsonPropertyName("@context")]
        public ContextSpecifier[]? Context { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("schemaDefinitions")]
        public Dictionary<string, DataSchema>? SchemaDefinitions { get; set; }

        [JsonPropertyName("forms")]
        public Form[]? Forms { get; set; }

        [JsonPropertyName("actions")]
        public Dictionary<string, Action>? Actions { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, Property>? Properties { get; set; }

        [JsonPropertyName("events")]
        public Dictionary<string, Event>? Events { get; set; }
    }
}
