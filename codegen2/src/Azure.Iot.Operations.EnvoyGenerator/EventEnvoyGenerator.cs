namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class EventEnvoyGenerator
    {
        internal static List<EventSpec> GenerateEventEnvoys(ErrorReporter errorReporter, TDThing tdThing, SchemaNamer schemaNamer, CodeName serviceName, EnvoyTransformFactory envoyFactory, Dictionary<string, IEnvoyTemplateTransform> transforms, HashSet<string> typesToSerialize)
        {
            List<EventSpec> eventSpecs = new();

            foreach (KeyValuePair<string, ValueTracker<TDEvent>> eventKvp in tdThing.Events?.Entries ?? new())
            {
                TDEvent eachEvent = eventKvp.Value.Value;
                FormInfo? subEventForm = FormInfo.CreateFromForm(errorReporter, eachEvent.Forms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements?.Any(e => e.Value.Value == TDValues.OpSubEvent) ?? false)?.Value, tdThing.SchemaDefinitions?.Entries);
                subEventForm ??= FormInfo.CreateFromForm(errorReporter, eachEvent.Forms?.Elements?.FirstOrDefault(f => f.Value.Op == null)?.Value, tdThing.SchemaDefinitions?.Entries);

                if (subEventForm != null && subEventForm.Format != SerializationFormat.None && subEventForm.TopicPattern != null)
                {
                    string schemaType = schemaNamer.GetEventSchema(eventKvp.Key);
                    typesToSerialize.Add(schemaType);
                    eventSpecs.Add(new EventSpec(schemaNamer, eventKvp.Key, schemaType));
                    foreach (IEnvoyTemplateTransform transform in envoyFactory.GetEventTransforms(schemaNamer, tdThing.Id!.Value!.Value, serviceName, eventKvp.Key, schemaType, subEventForm.Format, subEventForm.ServiceGroupId, subEventForm.TopicPattern))
                    {
                        transforms[transform.FileName] = transform;
                    }
                }
            }

            FormInfo? subAllEventsForm = FormInfo.CreateFromForm(errorReporter, tdThing.Forms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements?.Any(e => e.Value.Value == TDValues.OpSubAllEvents) ?? false)?.Value, tdThing.SchemaDefinitions?.Entries);
            if (subAllEventsForm != null && subAllEventsForm.Format != SerializationFormat.None && subAllEventsForm.TopicPattern != null)
            {
                typesToSerialize.Add(schemaNamer.AggregateEventSchema);
                eventSpecs.Add(new EventSpec(schemaNamer, schemaNamer.AggregateEventName, schemaNamer.AggregateEventSchema));
                foreach (IEnvoyTemplateTransform transform in envoyFactory.GetEventTransforms(schemaNamer, tdThing.Id!.Value!.Value, serviceName, schemaNamer.AggregateEventName, schemaNamer.AggregateEventSchema, subAllEventsForm.Format, subAllEventsForm.ServiceGroupId, subAllEventsForm.TopicPattern))
                {
                    transforms[transform.FileName] = transform;
                }
            }

            return eventSpecs;
        }
    }
}
