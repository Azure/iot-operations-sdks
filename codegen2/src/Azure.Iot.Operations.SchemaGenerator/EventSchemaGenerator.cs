namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class EventSchemaGenerator
    {
        internal static void GenerateEventSchemas(TDThing tdThing, string dirName, SchemaNamer schemaNamer, string projectName, Dictionary<string, SchemaSpec> schemaSpecs, Dictionary<string, HashSet<SerializationFormat>> referencedSchemas)
        {
            FormInfo? subAllEventsForm = FormInfo.CreateFromForm(tdThing.Forms?.FirstOrDefault(f => f.Op?.Values.Contains(TDValues.OpSubAllEvents) ?? false), tdThing.SchemaDefinitions);

            Dictionary<string, FieldSpec> valueFields = new();

            if (tdThing.Events != null)
            {
                foreach (KeyValuePair<string, TDEvent> eventKvp in tdThing.Events.Where(e => e.Value.Data != null))
                {
                    ProcessEvent(
                        schemaNamer,
                        eventKvp.Key,
                        eventKvp.Value,
                        projectName,
                        dirName,
                        tdThing.SchemaDefinitions,
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
            SchemaNamer schemaNamer,
            string eventName,
            TDEvent tdEvent,
            string projectName,
            string dirName,
            Dictionary<string, TDDataSchema>? schemaDefinitions,
            Dictionary<string, SchemaSpec> schemaSpecs,
            Dictionary<string, FieldSpec> valueFields)
        {
            FormInfo? subEventForm = FormInfo.CreateFromForm(tdEvent.Forms?.FirstOrDefault(f => f.Op?.Values.Contains(TDValues.OpSubEvent) ?? false), schemaDefinitions);
            subEventForm ??= FormInfo.CreateFromForm(tdEvent.Forms?.FirstOrDefault(f => f.Op == null), schemaDefinitions);

            FieldSpec dataFieldSpec = new(
                tdEvent.Description ?? $"The '{eventName}' Event data value.",
                tdEvent.Data!,
                BackupSchemaName: schemaNamer.GetEventValueSchema(eventName),
                Require: true,
                Base: dirName,
                Fragment: tdEvent.Placeholder);
            valueFields[eventName] = dataFieldSpec with { Require = false };

            if (subEventForm?.TopicPattern != null)
            {
                string eventSchemaName = schemaNamer.GetEventSchema(eventName);
                ObjectSpec eventObjectSpec = new(
                    tdEvent.Description ?? $"Container for the '{eventName}' Event data.",
                    new Dictionary<string, FieldSpec> { { eventName, dataFieldSpec } },
                    subEventForm.Format,
                    eventSchemaName);
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
                        schemaNamer.AggregateEventSchema);
                }
            }
        }
    }
}
