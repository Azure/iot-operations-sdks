namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    public static class ThingSupport
    {
        public static List<SerializationFormat> GetSerializationFormats(ErrorReporter errorReporter, List<TDThing> things)
        {
            HashSet<SerializationFormat> formats = new();

            foreach (TDThing thing in things)
            {
                AddFormatsFromLinks(errorReporter, thing.Links?.Elements, formats);
                AddFormatsFromForms(errorReporter, thing.Forms?.Elements, formats);

                foreach (KeyValuePair<string, ValueTracker<TDAction>> actionKvp in thing.Actions?.Entries ?? new())
                {
                    TDAction? action = actionKvp.Value.Value;
                    if (action != null)
                    {
                        AddFormatsFromForms(errorReporter, action.Forms?.Elements, formats);
                    }
                }

                foreach (KeyValuePair<string, ValueTracker<TDProperty>> propKvp in thing.Properties?.Entries ?? new())
                {
                    TDProperty? property = propKvp.Value.Value;
                    if (property != null)
                    {
                        AddFormatsFromForms(errorReporter, property.Forms?.Elements, formats);
                    }
                }

                foreach (KeyValuePair<string, ValueTracker<TDEvent>> eventKvp in thing.Events?.Entries ?? new())
                {
                    TDEvent? eachEvent = eventKvp.Value.Value;
                    if (eachEvent != null)
                    {
                        AddFormatsFromForms(errorReporter, eachEvent.Forms?.Elements, formats);
                    }
                }
            }

            return formats.ToList();
        }

        public static SerializationFormat ContentTypeToFormat(ErrorReporter errorReporter, ValueTracker<StringHolder>? contentType)
        {
            if (contentType?.Value == null)
            {
                return SerializationFormat.None;
            }

            switch (contentType.Value.Value)
            {
                case TDValues.ContentTypeJson:
                    return SerializationFormat.Json;
            }

            errorReporter.ReportError($"Unsupported content type '{contentType.Value.Value}'.", contentType.TokenIndex);

            return SerializationFormat.None;
        }

        private static void AddFormatsFromForms(ErrorReporter errorReporter, IEnumerable<ValueTracker<TDForm>>? forms, HashSet<SerializationFormat> formats)
        {
            if (forms == null)
            {
                return;
            }

            foreach (ValueTracker<TDForm> form in forms)
            {
                AddFormatFromContentType(errorReporter, form.Value.ContentType, formats);
                AddFormatsFromSchemaReferences(errorReporter, form.Value.AdditionalResponses?.Elements, formats);
                AddFormatsFromSchemaReferences(errorReporter, form.Value.HeaderInfo?.Elements, formats);
            }
        }

        private static void AddFormatsFromLinks(ErrorReporter errorReporter, IEnumerable<ValueTracker<TDLink>>? links, HashSet<SerializationFormat> formats)
        {
            if (links != null)
            {
                foreach (ValueTracker<TDLink> link in links)
                {
                    AddFormatFromContentType(errorReporter, link.Value.Type, formats);
                }
            }
        }

        private static void AddFormatsFromSchemaReferences(ErrorReporter errorReporter, IEnumerable<ValueTracker<TDSchemaReference>>? schemaRefs, HashSet<SerializationFormat> formats)
        {
            if (schemaRefs != null)
            {
                foreach (ValueTracker<TDSchemaReference> resp in schemaRefs)
                {
                    AddFormatFromContentType(errorReporter, resp.Value.ContentType, formats);
                }
            }
        }

        private static void AddFormatFromContentType(ErrorReporter errorReporter, ValueTracker<StringHolder>? contentType, HashSet<SerializationFormat> formats)
        {
            if (contentType?.Value != null)
            {
                formats.Add(ContentTypeToFormat(errorReporter, contentType));
            }
        }
    }
}
