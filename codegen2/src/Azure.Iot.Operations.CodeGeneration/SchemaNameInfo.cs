namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Text.Json.Serialization;

    public class SchemaNameInfo
    {
        [JsonPropertyName("suppressTitles")]
        public bool SuppressTitles { get; set; }

        [JsonPropertyName("constantsSchema")]
        public string? ConstantsSchema { get; set; }

        [JsonPropertyName("aggregateEventName")]
        public string? AggregateEventName { get; set; }

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

        [JsonPropertyName("eventSchema")]
        public FuncInfo? EventSchema { get; set; }

        [JsonPropertyName("eventSenderBinder")]
        public FuncInfo? EventSenderBinder { get; set; }

        [JsonPropertyName("eventReceiverBinder")]
        public FuncInfo? EventReceiverBinder { get; set; }

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

        [JsonPropertyName("actionExecutorBinder")]
        public FuncInfo? ActionExecutorBinder { get; set; }

        [JsonPropertyName("actionInvokerBinder")]
        public FuncInfo? ActionInvokerBinder { get; set; }

        [JsonPropertyName("backupSchemaName")]
        public FuncInfo? BackupSchemaName { get; set; }

        [JsonPropertyName("propRespErrorField")]
        public FuncInfo? PropRespErrorField { get; set; }

        [JsonPropertyName("actionRespErrorField")]
        public FuncInfo? ActionRespErrorField { get; set; }
    }
}
