namespace Azure.Iot.Operations.TDParser.Model
{
    using System.Text.Json.Serialization;

    public class Property : DataSchema
    {
        [JsonPropertyName("readOnly")]
        public bool ReaadOnly { get; set; }

        [JsonPropertyName("forms")]
        public Form[]? Forms { get; set; }
    }
}
