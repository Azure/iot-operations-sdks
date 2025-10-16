namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class EventEnvoyGenerator
    {
        internal static void GenerateEventEnvoys(TDThing tdThing, SchemaNamer schemaNamer, EnvoyTransformFactory envoyFactory, List<IEnvoyTemplateTransform> transforms)
        {
            foreach (KeyValuePair<string, TDEvent> eventKvp in tdThing.Events ?? new())
            {
                FormInfo? subEventForm = FormInfo.CreateFromForm(eventKvp.Value.Forms?.FirstOrDefault(f => f.Op == TDValues.OpSubEvent), tdThing.SchemaDefinitions);
                if (subEventForm != null && subEventForm.Format != SerializationFormat.None && subEventForm.TopicPattern != null)
                {
                    foreach (IEnvoyTemplateTransform transform in envoyFactory.GetEventTransforms(eventKvp.Key, schemaNamer.GetEventSchema(eventKvp.Key), subEventForm.Format, subEventForm.ServiceGroupId, subEventForm.TopicPattern))
                    {
                        transforms.Add(transform);
                    }
                }
            }

            FormInfo? subAllEventsForm = FormInfo.CreateFromForm(tdThing.Forms?.FirstOrDefault(f => f.Op == TDValues.OpSubAllEvents), tdThing.SchemaDefinitions);
            if (subAllEventsForm != null && subAllEventsForm.Format != SerializationFormat.None && subAllEventsForm.TopicPattern != null)
            {
                foreach (IEnvoyTemplateTransform transform in envoyFactory.GetEventTransforms(schemaNamer.AggregateEventSchema, schemaNamer.AggregateEventSchema, subAllEventsForm.Format, subAllEventsForm.ServiceGroupId, subAllEventsForm.TopicPattern))
                {
                    transforms.Add(transform);
                }
            }
        }
    }
}
