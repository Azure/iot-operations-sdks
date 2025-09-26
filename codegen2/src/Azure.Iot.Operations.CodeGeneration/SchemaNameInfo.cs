namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Text.Json.Serialization;

    public class SchemaNameInfo
    {
        [JsonPropertyName("aggregateEventSchema")]
        public string? AggregateEventSchema { get; set; }

        [JsonPropertyName("aggregatePropName")]
        public string? AggregatePropName { get; set; }

        [JsonPropertyName("aggregatePropSchema")]
        public string? AggregatePropSchema { get; set; }

        [JsonPropertyName("aggregatePropWriteSchema")]
        public string? AggregatePropWriteSchema { get; set; }

        [JsonPropertyName("aggregatePropReadRespSchema")]
        public string? AggregatePropReadRespSchema { get; set; }

        [JsonPropertyName("aggregatePropWriteRespSchema")]
        public string? AggregatePropWriteRespSchema { get; set; }

        [JsonPropertyName("aggregatePropReadErrSchema")]
        public string? AggregatePropReadErrSchema { get; set; }

        [JsonPropertyName("aggregatePropWriteErrSchema")]
        public string? AggregatePropWriteErrSchema { get; set; }

        [JsonPropertyName("aggregateReadRespValueField")]
        public string? AggregateReadRespValueField { get; set; }

        [JsonPropertyName("aggregateRespErrorField")]
        public string? AggregateRespErrorField { get; set; }

        [JsonPropertyName("propRespErrorField")]
        public string? PropRespErrorField { get; set; }

        [JsonPropertyName("actionRespErrorField")]
        public string? ActionRespErrorField { get; set; }

        [JsonPropertyName("eventSchema")]
        public FuncInfo? EventSchema { get; set; }

        [JsonPropertyName("eventValueSchema")]
        public FuncInfo? EventValueSchema { get; set; }

        [JsonPropertyName("propSchema")]
        public FuncInfo? PropSchema { get; set; }

        [JsonPropertyName("writablePropSchema")]
        public FuncInfo? WritablePropSchema { get; set; }

        [JsonPropertyName("propReadRespSchema")]
        public FuncInfo? PropReadRespSchema { get; set; }

        [JsonPropertyName("propWriteRespSchema")]
        public FuncInfo? PropWriteRespSchema { get; set; }

        [JsonPropertyName("propValueSchema")]
        public FuncInfo? PropValueSchema { get; set; }

        [JsonPropertyName("actionInSchema")]
        public FuncInfo? ActionInSchema { get; set; }

        [JsonPropertyName("actionOutSchema")]
        public FuncInfo? ActionOutSchema { get; set; }

        [JsonPropertyName("actionRespSchema")]
        public FuncInfo? ActionRespSchema { get; set; }

        [JsonPropertyName("backupSchemaName")]
        public FuncInfo? BackupSchemaName { get; set; }
    }
}
