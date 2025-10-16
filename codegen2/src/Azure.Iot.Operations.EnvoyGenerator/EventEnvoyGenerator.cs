namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class EventEnvoyGenerator
    {
        internal static List<EventSpec> GenerateEventEnvoys(TDThing tdThing, SchemaNamer schemaNamer, CodeName serviceName, EnvoyTransformFactory envoyFactory, Dictionary<string, IEnvoyTemplateTransform> transforms, HashSet<string> typesToSerialize)
        {
            List<EventSpec> eventSpecs = new();

            foreach (KeyValuePair<string, TDEvent> eventKvp in tdThing.Events ?? new())
            {
                FormInfo? subEventForm = FormInfo.CreateFromForm(eventKvp.Value.Forms?.FirstOrDefault(f => f.Op?.Values.Contains(TDValues.OpSubEvent) ?? false), tdThing.SchemaDefinitions);
                subEventForm ??= FormInfo.CreateFromForm(eventKvp.Value.Forms?.FirstOrDefault(f => f.Op == null), tdThing.SchemaDefinitions);

                if (subEventForm != null && subEventForm.Format != SerializationFormat.None && subEventForm.TopicPattern != null)
                {
                    string schemaType = schemaNamer.GetEventSchema(eventKvp.Key);
                    typesToSerialize.Add(schemaType);
                    eventSpecs.Add(new EventSpec(schemaNamer, eventKvp.Key, schemaType));
                    foreach (IEnvoyTemplateTransform transform in envoyFactory.GetEventTransforms(schemaNamer, tdThing.Id!, serviceName, eventKvp.Key, schemaType, subEventForm.Format, subEventForm.ServiceGroupId, subEventForm.TopicPattern))
                    {
                        transforms[transform.FileName] = transform;
                    }
                }
            }

            FormInfo? subAllEventsForm = FormInfo.CreateFromForm(tdThing.Forms?.FirstOrDefault(f => f.Op?.Values.Contains(TDValues.OpSubAllEvents) ?? false), tdThing.SchemaDefinitions);
            if (subAllEventsForm != null && subAllEventsForm.Format != SerializationFormat.None && subAllEventsForm.TopicPattern != null)
            {
                typesToSerialize.Add(schemaNamer.AggregateEventSchema);
                eventSpecs.Add(new EventSpec(schemaNamer, schemaNamer.AggregateEventName, schemaNamer.AggregateEventSchema));
                foreach (IEnvoyTemplateTransform transform in envoyFactory.GetEventTransforms(schemaNamer, tdThing.Id!, serviceName, schemaNamer.AggregateEventName, schemaNamer.AggregateEventSchema, subAllEventsForm.Format, subAllEventsForm.ServiceGroupId, subAllEventsForm.TopicPattern))
                {
                    transforms[transform.FileName] = transform;
                }
            }

            return eventSpecs;
        }
    }
}
