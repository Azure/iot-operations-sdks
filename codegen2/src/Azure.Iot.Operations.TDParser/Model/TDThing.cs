namespace Azure.Iot.Operations.TDParser.Model
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class TDThing
    {
        [JsonPropertyName("@context")]
        public TDContextSpecifier[]? Context { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("links")]
        public List<TDLink>? Links { get; set; }

        [JsonPropertyName("schemaDefinitions")]
        public Dictionary<string, TDDataSchema>? SchemaDefinitions { get; set; }

        [JsonPropertyName("forms")]
        public TDForm[]? Forms { get; set; }

        [JsonPropertyName("actions")]
        public Dictionary<string, TDAction>? Actions { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, TDProperty>? Properties { get; set; }

        [JsonPropertyName("events")]
        public Dictionary<string, TDEvent>? Events { get; set; }
    }
}
