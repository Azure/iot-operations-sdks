namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class EventEnvoyGenerator
    {
        internal static void GenerateEventEnvoys(TDThing tdThing, SchemaNamer schemaNamer, EnvoyTransformFactory envoyFactory, Dictionary<string, IEnvoyTemplateTransform> transforms, HashSet<string> typesToSerialize)
        {
            foreach (KeyValuePair<string, TDEvent> eventKvp in tdThing.Events ?? new())
            {
                FormInfo? subEventForm = FormInfo.CreateFromForm(eventKvp.Value.Forms?.FirstOrDefault(f => f.Op == TDValues.OpSubEvent), tdThing.SchemaDefinitions);
                if (subEventForm != null && subEventForm.Format != SerializationFormat.None && subEventForm.TopicPattern != null)
                {
                    string? schemaType = schemaNamer.GetEventSchema(eventKvp.Key);
                    typesToSerialize.Add(schemaType);
                    foreach (IEnvoyTemplateTransform transform in envoyFactory.GetEventTransforms(schemaNamer, tdThing.Id!, eventKvp.Key, schemaNamer.GetEventSchema(eventKvp.Key), subEventForm.Format, subEventForm.ServiceGroupId, subEventForm.TopicPattern))
                    {
                        transforms[transform.FileName] = transform;
                    }
                }
            }

            FormInfo? subAllEventsForm = FormInfo.CreateFromForm(tdThing.Forms?.FirstOrDefault(f => f.Op == TDValues.OpSubAllEvents), tdThing.SchemaDefinitions);
            if (subAllEventsForm != null && subAllEventsForm.Format != SerializationFormat.None && subAllEventsForm.TopicPattern != null)
            {
                typesToSerialize.Add(schemaNamer.AggregateEventSchema);
                foreach (IEnvoyTemplateTransform transform in envoyFactory.GetEventTransforms(schemaNamer, tdThing.Id!, schemaNamer.AggregateEventName, schemaNamer.AggregateEventSchema, subAllEventsForm.Format, subAllEventsForm.ServiceGroupId, subAllEventsForm.TopicPattern))
                {
                    transforms[transform.FileName] = transform;
                }
            }
        }
    }
}
