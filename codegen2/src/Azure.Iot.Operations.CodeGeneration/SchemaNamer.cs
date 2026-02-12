// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text.Json;
    using Azure.Iot.Operations.TDParser.Model;

    public class SchemaNamer
    {
        private readonly string? schemaPrefix;
        private readonly SchemaNameInfo? schemaNameInfo;
        private readonly bool suppressTitles;

        public SchemaNamer(string? schemaPrefix, string? schemaNameInfoText = null)
        {
            this.schemaPrefix = schemaPrefix;
            this.schemaNameInfo = schemaNameInfoText != null ? JsonSerializer.Deserialize<SchemaNameInfo>(schemaNameInfoText) : null;
            this.suppressTitles = this.schemaNameInfo?.SuppressTitles ?? false;
        }

        public string ConstantsSchema { get => Select(this.schemaPrefix, this.schemaNameInfo?.ConstantsSchema, "Constants"); }

        public string AggregateEventName { get => Select(null, this.schemaNameInfo?.AggregateEventName, "Events"); }

        public string AggregateEventSchema { get => Select(this.schemaPrefix, this.schemaNameInfo?.AggregateEventSchema, "EventCollection"); }

        public string AggregatePropName { get => Select(null, this.schemaNameInfo?.AggregatePropName, "Properties"); }

        public string AggregatePropSchema { get => Select(this.schemaPrefix, this.schemaNameInfo?.AggregatePropSchema, "PropertyCollection"); }

        public string AggregatePropWriteSchema { get => Select(this.schemaPrefix, this.schemaNameInfo?.AggregatePropWriteSchema, "PropertyUpdate"); }

        public string AggregatePropReadRespSchema { get => Select(this.schemaPrefix, this.schemaNameInfo?.AggregatePropReadRespSchema, "PropertyCollectionReadResponseSchema"); }

        public string AggregatePropWriteRespSchema { get => Select(this.schemaPrefix, this.schemaNameInfo?.AggregatePropWriteRespSchema, "PropertyCollectionWriteResponseSchema"); }

        public string AggregatePropReadErrSchema { get => Select(this.schemaPrefix, this.schemaNameInfo?.AggregatePropReadErrSchema, "PropertyCollectionReadError"); }

        public string AggregatePropWriteErrSchema { get => Select(this.schemaPrefix, this.schemaNameInfo?.AggregatePropWriteErrSchema, "PropertyCollectionWriteError"); }

        public string ReadRequesterBinder { get => Select(null, this.schemaNameInfo?.ReadRequesterBinder, "ReadRequester"); }

        public string ReadResponderBinder { get => Select(null, this.schemaNameInfo?.ReadResponderBinder, "ReadResponder"); }

        public string WriteRequesterBinder { get => Select(null, this.schemaNameInfo?.WriteRequesterBinder, "WriteRequester"); }

        public string WriteResponderBinder { get => Select(null, this.schemaNameInfo?.WriteResponderBinder, "WriteResponder"); }

        public string AggregateReadRespValueField { get => Select(null, this.schemaNameInfo?.AggregateReadRespValueField, "_properties"); }

        public string AggregateRespErrorField { get => Select(null, this.schemaNameInfo?.AggregateRespErrorField, "_errors"); }

        public string GetEventSchema(string eventName) => Expand(this.schemaPrefix, null, this.schemaNameInfo?.EventSchema, $"{Cap(eventName)}Event", eventName);

        public string GetEventValueSchema(string eventName) => Expand(this.schemaPrefix, null, this.schemaNameInfo?.EventValueSchema, $"{Cap(eventName)}Value", eventName);

        public string GetEventSenderBinder(string eventSchema) => Expand(null, null, this.schemaNameInfo?.EventSenderBinder, $"{Cap(eventSchema)}Sender", eventSchema);

        public string GetEventReceiverBinder(string eventSchema) => Expand(null, null, this.schemaNameInfo?.EventReceiverBinder, $"{Cap(eventSchema)}Receiver", eventSchema);

        public string GetPropSchema(string propName) => Expand(this.schemaPrefix, null, this.schemaNameInfo?.PropSchema, $"{Cap(propName)}Property", propName);

        public string GetWritablePropSchema(string propName) => Expand(this.schemaPrefix, null, this.schemaNameInfo?.WritablePropSchema, $"{Cap(propName)}WritableProperty", propName);

        public string GetPropReadRespSchema(string propName) => Expand(this.schemaPrefix, null, this.schemaNameInfo?.PropReadRespSchema, $"{Cap(propName)}ReadResponseSchema", propName);

        public string GetPropWriteRespSchema(string propName) => Expand(this.schemaPrefix, null, this.schemaNameInfo?.PropWriteRespSchema, $"{Cap(propName)}WriteResponseSchema", propName);

        public string GetPropValueSchema(string propName) => Expand(this.schemaPrefix, null, this.schemaNameInfo?.PropValueSchema, $"Property{Cap(propName)}Value", propName);

        public string GetPropReadActName(string propName) => Expand(null, null, this.schemaNameInfo?.PropReadActName, $"Read{Cap(propName)}", propName);

        public string GetPropWriteActName(string propName) => Expand(null, null, this.schemaNameInfo?.PropWriteActName, $"Write{Cap(propName)}", propName);

        public string GetPropMaintainerBinder(string propSchema) => Expand(null, null, this.schemaNameInfo?.PropMaintainerBinder, $"{Cap(propSchema)}Maintainer", propSchema);

        public string GetPropConsumerBinder(string propSchema) => Expand(null, null, this.schemaNameInfo?.PropConsumerBinder, $"{Cap(propSchema)}Consumer", propSchema);

        public string GetActionInSchema(TDDataSchema? dataSchema, string actionName) => Expand(this.schemaPrefix, dataSchema, this.schemaNameInfo?.ActionInSchema, $"{Cap(actionName)}InputArguments", actionName);

        public string GetActionOutSchema(TDDataSchema? dataSchema, string actionName) => Expand(this.schemaPrefix, dataSchema, this.schemaNameInfo?.ActionOutSchema, $"{Cap(actionName)}OutputArguments", actionName);

        public string GetActionRespSchema(string actionName) => Expand(this.schemaPrefix, null, this.schemaNameInfo?.ActionRespSchema, $"{Cap(actionName)}ResponseSchema", actionName);

        public string GetActionExecutorBinder(string actionName) => Expand(this.schemaPrefix, null, this.schemaNameInfo?.ActionExecutorBinder, $"{Cap(actionName)}ActionExecutor", actionName);

        public string GetActionInvokerBinder(string actionName) => Expand(this.schemaPrefix, null, this.schemaNameInfo?.ActionInvokerBinder, $"{Cap(actionName)}ActionInvoker", actionName);

        public string GetPropReadRespErrorField(string propName, string errorSchemaName) => Expand(null, null, this.schemaNameInfo?.PropReadRespErrorField, "_error", propName, errorSchemaName);

        public string GetPropWriteRespErrorField(string propName, string errorSchemaName) => Expand(null, null, this.schemaNameInfo?.PropWriteRespErrorField, "_error", propName, errorSchemaName);

        public string GetActionRespErrorField(string actionName, string errorSchemaName) => Expand(null, null, this.schemaNameInfo?.ActionRespErrorField, "_error", actionName, errorSchemaName);

        public string GetBackupSchemaName(string parentSchemaName, string childName) => Expand(null, null, this.schemaNameInfo?.BackupSchemaName, $"{Cap(parentSchemaName)}{Cap(childName)}", parentSchemaName, childName);

        public string ApplyBackupSchemaName(string? title, string backupName) => ChooseTitleOrName(title, backupName);

        [return: NotNullIfNotNull(nameof(name))]
        public string? ChooseTitleOrName(string? title, string? name) => this.suppressTitles ? name : title ?? name;

        private string Select(string? prefix, string? configValue, string defaultOut)
        {
            return Prefix(prefix, configValue ?? defaultOut);
        }

        private string Expand(string? prefix, TDDataSchema? dataSchema, FuncInfo? funcInfo, string defaultOut, params string[] args)
        {
            if (!this.suppressTitles && dataSchema?.Ref == null && dataSchema?.Title?.Value != null)
            {
                return Prefix(prefix, dataSchema.Title.Value.Value);
            }

            if (funcInfo == null || funcInfo.Output == null || funcInfo.Input == null || funcInfo.Input.Length < args.Length)
            {
                return Prefix(prefix, defaultOut);
            }

            string outString = funcInfo.Output;
            foreach (int ix in Enumerable.Range(0, funcInfo.Input.Length))
            {
                outString = outString.Replace($"{{{funcInfo.Input[ix]}}}", MaybeCap(args[ix], funcInfo.Capitalize));
            }

            return Prefix(prefix, outString);
        }

        private string Prefix(string? prefix, string name)
        {
            string sep = char.IsLower(name[0]) ? "_" : "";
            return prefix != null ? $"{prefix}{sep}{name}" : name;
        }

        private static string Cap(string input) => input.Length == 0 ? input : char.ToUpper(input[0]) + input.Substring(1);

        private static string MaybeCap(string input, bool capitalize) => capitalize ? Cap(input) : input;
    }
}
