namespace Azure.Iot.Operations.TDParser.Model
{
    using System.Text.Json.Serialization;

    public class TDProperty : TDDataSchema
    {
        [JsonPropertyName("readOnly")]
        public bool ReadOnly { get; set; }

        [JsonPropertyName("dtv:placeholder")]
        public bool Placeholder { get; set; }

        [JsonPropertyName("forms")]
        public TDForm[]? Forms { get; set; }
    }
}
