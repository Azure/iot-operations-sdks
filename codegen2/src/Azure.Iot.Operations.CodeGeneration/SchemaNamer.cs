namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json;
    using System.Linq;

    public class SchemaNamer
    {
        private SchemaNameInfo? schemaNameInfo;
        private bool suppressTitles;

        public SchemaNamer(string? schemaNameInfoText)
        {
            this.schemaNameInfo = schemaNameInfoText != null ? JsonSerializer.Deserialize<SchemaNameInfo>(schemaNameInfoText) : null;
            this.suppressTitles = this.schemaNameInfo?.SuppressTitles ?? false;
        }

        public string ConstantsSchema { get => this.schemaNameInfo?.ConstantsSchema ?? "Constants"; }

        public string AggregateEventName { get => this.schemaNameInfo?.AggregateEventName ?? "Events"; }

        public string AggregateEventSchema { get => this.schemaNameInfo?.AggregateEventSchema ?? "EventCollection"; }

        public string AggregatePropName { get => this.schemaNameInfo?.AggregatePropName ?? "Properties"; }

        public string AggregatePropSchema { get => this.schemaNameInfo?.AggregatePropSchema ?? "PropertyCollection"; }

        public string AggregatePropWriteSchema { get => this.schemaNameInfo?.AggregatePropWriteSchema ?? "PropertyUpdate"; }

        public string AggregatePropReadRespSchema { get => this.schemaNameInfo?.AggregatePropReadRespSchema ?? "PropertyCollectionReadResponseSchema"; }

        public string AggregatePropWriteRespSchema { get => this.schemaNameInfo?.AggregatePropWriteRespSchema ?? "PropertyCollectionWriteResponseSchema"; }

        public string AggregatePropReadErrSchema { get => this.schemaNameInfo?.AggregatePropReadErrSchema ?? "PropertyCollectionReadError"; }

        public string AggregatePropWriteErrSchema { get => this.schemaNameInfo?.AggregatePropWriteErrSchema ?? "PropertyCollectionWriteError"; }

        public string ReadRequesterBinder { get => this.schemaNameInfo?.ReadRequesterBinder ?? "ReadRequester"; }

        public string ReadResponderBinder { get => this.schemaNameInfo?.ReadResponderBinder ?? "ReadResponder"; }

        public string WriteRequesterBinder { get => this.schemaNameInfo?.WriteRequesterBinder ?? "WriteRequester"; }

        public string WriteResponderBinder { get => this.schemaNameInfo?.WriteResponderBinder ?? "WriteResponder"; }

        public string AggregateReadRespValueField { get => this.schemaNameInfo?.AggregateReadRespValueField ?? "_properties"; }

        public string AggregateRespErrorField { get => this.schemaNameInfo?.AggregateRespErrorField ?? "_errors"; }

        public string GetEventSchema(string eventName) => Expand(null, this.schemaNameInfo?.EventSchema, $"{Cap(eventName)}Event", eventName);

        public string GetEventValueSchema(string eventSchema) => Expand(null, this.schemaNameInfo?.EventValueSchema, $"{Cap(eventSchema)}Value", eventSchema);

        public string GetEventSenderBinder(string eventSchema) => Expand(null, this.schemaNameInfo?.EventSenderBinder, $"{Cap(eventSchema)}Sender", eventSchema);

        public string GetEventReceiverBinder(string eventSchema) => Expand(null, this.schemaNameInfo?.EventReceiverBinder, $"{Cap(eventSchema)}Receiver", eventSchema);

        public string GetPropSchema(string propName) => Expand(null, this.schemaNameInfo?.PropSchema, $"{Cap(propName)}Property", propName);

        public string GetWritablePropSchema(string propName) => Expand(null, this.schemaNameInfo?.WritablePropSchema, $"{Cap(propName)}WritableProperty", propName);

        public string GetPropReadRespSchema(string propName) => Expand(null, this.schemaNameInfo?.PropReadRespSchema, $"{Cap(propName)}ReadResponseSchema", propName);

        public string GetPropWriteRespSchema(string propName) => Expand(null, this.schemaNameInfo?.PropWriteRespSchema, $"{Cap(propName)}WriteResponseSchema", propName);

        public string GetPropValueSchema(string propName) => Expand(null, this.schemaNameInfo?.PropValueSchema, $"Property{Cap(propName)}Value", propName);

        public string GetPropReadActName(string propName) => Expand(null, this.schemaNameInfo?.PropReadActName, $"Read{Cap(propName)}", propName);

        public string GetPropWriteActName(string propName) => Expand(null, this.schemaNameInfo?.PropWriteActName, $"Write{Cap(propName)}", propName);

        public string GetPropMaintainerBinder(string propSchema) => Expand(null, this.schemaNameInfo?.PropMaintainerBinder, $"{Cap(propSchema)}Maintainer", propSchema);

        public string GetPropConsumerBinder(string propSchema) => Expand(null, this.schemaNameInfo?.PropConsumerBinder, $"{Cap(propSchema)}Consumer", propSchema);

        public string GetActionInSchema(string? title, string actionName) => Expand(title, this.schemaNameInfo?.ActionInSchema, $"{Cap(actionName)}InputArguments", actionName);

        public string GetActionOutSchema(string? title, string actionName) => Expand(title, this.schemaNameInfo?.ActionOutSchema, $"{Cap(actionName)}OutputArguments", actionName);

        public string GetActionRespSchema(string actionName) => Expand(null, this.schemaNameInfo?.ActionRespSchema, $"{Cap(actionName)}ResponseSchema", actionName);

        public string GetActionExecutorBinder(string actionName) => Expand(null, this.schemaNameInfo?.ActionExecutorBinder, $"{Cap(actionName)}ActionExecutor", actionName);

        public string GetActionInvokerBinder(string actionName) => Expand(null, this.schemaNameInfo?.ActionInvokerBinder, $"{Cap(actionName)}ActionInvoker", actionName);

        public string GetPropReadRespErrorField(string propName, string errorSchemaName) => Expand(null, this.schemaNameInfo?.PropReadRespErrorField, "_error", propName, errorSchemaName);

        public string GetPropWriteRespErrorField(string propName, string errorSchemaName) => Expand(null, this.schemaNameInfo?.PropWriteRespErrorField, "_error", propName, errorSchemaName);

        public string GetActionRespErrorField(string actionName, string errorSchemaName) => Expand(null, this.schemaNameInfo?.ActionRespErrorField, "_error", actionName, errorSchemaName);

        public string GetBackupSchemaName(string parentSchemaName, string childName) => Expand(null, this.schemaNameInfo?.BackupSchemaName, $"{Cap(parentSchemaName)}{Cap(childName)}", parentSchemaName, childName);

        public string ApplyBackupSchemaName(string? title, string backupName) => ChooseTitleOrName(title, backupName);

        [return: NotNullIfNotNull(nameof(name))]
        public string? ChooseTitleOrName(string? title, string? name) => this.suppressTitles ? name : title ?? name;

        private string Expand(string? title, FuncInfo? funcInfo, string defaultOut, params string[] args)
        {
            if (!this.suppressTitles && title != null)
            {
                return title;
            }

            if (funcInfo == null || funcInfo.Output == null || funcInfo.Input == null || funcInfo.Input.Length < args.Length)
            {
                return defaultOut;
            }

            string outString = funcInfo.Output;
            foreach (int ix in Enumerable.Range(0, funcInfo.Input.Length))
            {
                outString = outString.Replace($"{{{funcInfo.Input[ix]}}}", MaybeCap(args[ix], funcInfo.Capitalize));
            }

            return outString;
        }

        private static string Cap(string input) => input.Length == 0 ? input : char.ToUpper(input[0]) + input.Substring(1);

        private static string MaybeCap(string input, bool capitalize) => capitalize ? Cap(input) : input;
    }
}
