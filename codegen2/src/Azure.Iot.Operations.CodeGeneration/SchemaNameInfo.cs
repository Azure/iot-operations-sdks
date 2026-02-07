// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

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

        [JsonPropertyName("readRequesterBinder")]
        public string? ReadRequesterBinder { get; set; }

        [JsonPropertyName("readResponderBinder")]
        public string? ReadResponderBinder { get; set; }

        [JsonPropertyName("writeRequesterBinder")]
        public string? WriteRequesterBinder { get; set; }

        [JsonPropertyName("writeResponderBinder")]
        public string? WriteResponderBinder { get; set; }

        [JsonPropertyName("aggregateReadRespValueField")]
        public string? AggregateReadRespValueField { get; set; }

        [JsonPropertyName("aggregateRespErrorField")]
        public string? AggregateRespErrorField { get; set; }

        [JsonPropertyName("eventSchema")]
        public FuncInfo? EventSchema { get; set; }

        [JsonPropertyName("eventValueSchema")]
        public FuncInfo? EventValueSchema { get; set; }

        [JsonPropertyName("eventSenderBinder")]
        public FuncInfo? EventSenderBinder { get; set; }

        [JsonPropertyName("eventReceiverBinder")]
        public FuncInfo? EventReceiverBinder { get; set; }

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

        [JsonPropertyName("propReadActName")]
        public FuncInfo? PropReadActName { get; set; }

        [JsonPropertyName("propWriteActName")]
        public FuncInfo? PropWriteActName { get; set; }

        [JsonPropertyName("propMaintainerBinder")]
        public FuncInfo? PropMaintainerBinder { get; set; }

        [JsonPropertyName("propConsumerBinder")]
        public FuncInfo? PropConsumerBinder { get; set; }

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

        [JsonPropertyName("propReadRespErrorField")]
        public FuncInfo? PropReadRespErrorField { get; set; }

        [JsonPropertyName("propWriteRespErrorField")]
        public FuncInfo? PropWriteRespErrorField { get; set; }

        [JsonPropertyName("actionRespErrorField")]
        public FuncInfo? ActionRespErrorField { get; set; }
    }
}
