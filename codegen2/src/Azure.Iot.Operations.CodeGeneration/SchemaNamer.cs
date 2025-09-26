namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Text.Json;
    using System.Linq;

    public class SchemaNamer
    {
        private SchemaNameInfo? schemaNameInfo;

        public SchemaNamer(string? schemaNameInfoText)
        {
            this.schemaNameInfo = schemaNameInfoText != null ? JsonSerializer.Deserialize<SchemaNameInfo>(schemaNameInfoText) : null;
        }

        public string AggregateEventSchema { get => this.schemaNameInfo?.AggregateEventSchema ?? "EventCollection"; }

        public string AggregatePropName { get => this.schemaNameInfo?.AggregatePropName ?? "Properties"; }

        public string AggregatePropSchema { get => this.schemaNameInfo?.AggregatePropSchema ?? "PropertyCollection"; }

        public string AggregatePropWriteSchema { get => this.schemaNameInfo?.AggregatePropWriteSchema ?? "PropertyUpdate"; }

        public string AggregatePropReadRespSchema { get => this.schemaNameInfo?.AggregatePropReadRespSchema ?? "PropertyCollectionReadResponseSchema"; }

        public string AggregatePropWriteRespSchema { get => this.schemaNameInfo?.AggregatePropWriteRespSchema ?? "PropertyCollectionWriteResponseSchema"; }

        public string AggregatePropReadErrSchema { get => this.schemaNameInfo?.AggregatePropReadErrSchema ?? "PropertyCollectionReadError"; }

        public string AggregatePropWriteErrSchema { get => this.schemaNameInfo?.AggregatePropWriteErrSchema ?? "PropertyCollectionWriteError"; }

        public string AggregateReadRespValueField { get => this.schemaNameInfo?.AggregateReadRespValueField ?? "_properties"; }

        public string AggregateRespErrorField { get => this.schemaNameInfo?.AggregateRespErrorField ?? "_errors"; }

        public string PropRespErrorField { get => this.schemaNameInfo?.PropRespErrorField ?? "_error"; }

        public string ActionRespErrorField { get => this.schemaNameInfo?.ActionRespErrorField ?? "_error"; }

        public string GetEventSchema(string eventName) => Expand(this.schemaNameInfo?.EventSchema, $"{Cap(eventName)}Event", eventName);

        public string GetEventValueSchema(string eventName) => Expand(this.schemaNameInfo?.EventValueSchema, $"Event{Cap(eventName)}Value", eventName);

        public string GetPropSchema(string propName) => Expand(this.schemaNameInfo?.PropSchema, $"{Cap(propName)}Property", propName);

        public string GetWritablePropSchema(string propName) => Expand(this.schemaNameInfo?.WritablePropSchema, $"{Cap(propName)}WritableProperty", propName);

        public string GetPropReadRespSchema(string propName) => Expand(this.schemaNameInfo?.PropReadRespSchema, $"{Cap(propName)}ReadResponseSchema", propName);

        public string GetPropWriteRespSchema(string propName) => Expand(this.schemaNameInfo?.PropWriteRespSchema, $"{Cap(propName)}WriteResponseSchema", propName);

        public string GetPropValueSchema(string propName) => Expand(this.schemaNameInfo?.PropValueSchema, $"Property{Cap(propName)}Value", propName);

        public string GetActionInSchema(string actionName) => Expand(this.schemaNameInfo?.ActionInSchema, $"{Cap(actionName)}InputArguments", actionName);

        public string GetActionOutSchema(string actionName) => Expand(this.schemaNameInfo?.ActionOutSchema, $"{Cap(actionName)}OutputArguments", actionName);

        public string GetActionRespSchema(string actionName) => Expand(this.schemaNameInfo?.ActionRespSchema, $"{Cap(actionName)}ResponseSchema", actionName);

        public string GetBackupSchemaName(string parentSchemaName, string childName) => Expand(this.schemaNameInfo?.BackupSchemaName, $"{Cap(parentSchemaName)}{Cap(childName)}", parentSchemaName, childName);

        private static string Expand(FuncInfo? funcInfo, string defaultOut, params string[] args)
        {
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

        private static string Cap(string input) => char.ToUpper(input[0]) + input.Substring(1);

        private static string MaybeCap(string input, bool capitalize) => capitalize ? Cap(input) : input;
    }
}
