namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class EventSchemaGenerator
    {
        internal static void GenerateEventSchemas(ErrorReporter errorReporter, TDThing tdThing, string dirName, SchemaNamer schemaNamer, string projectName, Dictionary<string, SchemaSpec> schemaSpecs, Dictionary<string, HashSet<SerializationFormat>> referencedSchemas)
        {
            FormInfo? subAllEventsForm = FormInfo.CreateFromForm(errorReporter, tdThing.Forms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements?.Any(e => e.Value.Value == TDValues.OpSubAllEvents) ?? false)?.Value, tdThing.SchemaDefinitions?.Entries);

            Dictionary<string, FieldSpec> valueFields = new();

            if (tdThing.Events?.Entries != null)
            {
                foreach (KeyValuePair<string, ValueTracker<TDEvent>> eventKvp in tdThing.Events.Entries.Where(e => e.Value.Value.Data != null))
                {
                    ProcessEvent(
                        errorReporter,
                        schemaNamer,
                        eventKvp.Key,
                        eventKvp.Value.Value!,
                        projectName,
                        dirName,
                        tdThing.SchemaDefinitions?.Entries,
                        schemaSpecs,
                        valueFields);
                }
            }

            GenerateCollectiveEventObject(
                schemaNamer,
                subAllEventsForm,
                valueFields,
                schemaSpecs);
        }

        private static void ProcessEvent(
            ErrorReporter errorReporter,
            SchemaNamer schemaNamer,
            string eventName,
            TDEvent tdEvent,
            string projectName,
            string dirName,
            Dictionary<string, ValueTracker<TDDataSchema>>? schemaDefinitions,
            Dictionary<string, SchemaSpec> schemaSpecs,
            Dictionary<string, FieldSpec> valueFields)
        {
            FormInfo? subEventForm = FormInfo.CreateFromForm(errorReporter, tdEvent.Forms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements?.Any(e => e.Value.Value == TDValues.OpSubEvent) ?? false)?.Value, schemaDefinitions);
            subEventForm ??= FormInfo.CreateFromForm(errorReporter, tdEvent.Forms?.Elements?.FirstOrDefault(f => f.Value.Op == null)?.Value, schemaDefinitions);

            FieldSpec dataFieldSpec = new(
                tdEvent.Description?.Value.Value ?? $"The '{eventName}' Event data value.",
                tdEvent.Data!,
                BackupSchemaName: schemaNamer.GetEventValueSchema(eventName),
                Require: true,
                Base: dirName,
                Fragment: tdEvent.Placeholder?.Value.Value ?? false);
            valueFields[eventName] = dataFieldSpec with { Require = false };

            if (subEventForm?.TopicPattern != null)
            {
                string eventSchemaName = schemaNamer.GetEventSchema(eventName);
                ObjectSpec eventObjectSpec = new(
                    tdEvent.Description?.Value.Value ?? $"Container for the '{eventName}' Event data.",
                    new Dictionary<string, FieldSpec> { { eventName, dataFieldSpec } },
                    subEventForm.Format,
                    eventSchemaName,
                    TokenIndex: -1);
                schemaSpecs[eventSchemaName] = eventObjectSpec;
            }
        }

        private static void GenerateCollectiveEventObject(
            SchemaNamer schemaNamer,
            FormInfo? topLevelEventsForm,
            Dictionary<string, FieldSpec> valueFields,
            Dictionary<string, SchemaSpec> schemaSpecs)
        {
            if (topLevelEventsForm?.TopicPattern != null)
            {
                if (valueFields.Any())
                {
                    schemaSpecs[schemaNamer.AggregateEventSchema] = new ObjectSpec(
                        $"Data values of Events.",
                        valueFields,
                        topLevelEventsForm.Format,
                        schemaNamer.AggregateEventSchema,
                        TokenIndex: -1);
                }
            }
        }
    }
}
