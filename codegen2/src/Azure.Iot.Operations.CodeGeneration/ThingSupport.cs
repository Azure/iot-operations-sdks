namespace Azure.Iot.Operations.CodeGeneration
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.TDParser.Model;

    public static class ThingSupport
    {
        public static List<SerializationFormat> GetSerializationFormats(List<TDThing> things)
        {
            HashSet<SerializationFormat> formats = new();

            foreach (TDThing thing in things)
            {
                AddFormatsFromLinks(thing.Links, formats);
                AddFormatsFromForms(thing.Forms, formats);

                if (thing.Actions != null)
                {
                    foreach (TDAction action in thing.Actions.Values)
                    {
                        AddFormatsFromForms(action.Forms, formats);
                    }
                }

                if (thing.Properties != null)
                {
                    foreach (TDProperty property in thing.Properties.Values)
                    {
                        AddFormatsFromForms(property.Forms, formats);
                    }
                }

                if (thing.Events != null)
                {
                    foreach (TDEvent evt in thing.Events.Values)
                    {
                        AddFormatsFromForms(evt.Forms, formats);
                    }
                }
            }

            return formats.ToList();
        }

        public static SerializationFormat ContentTypeToFormat(string? contentType)
        {
            return contentType switch
            {
                TDValues.ContentTypeJson => SerializationFormat.Json,
                _ => SerializationFormat.None,
            };
        }

        private static void AddFormatsFromForms(IEnumerable<TDForm>? forms, HashSet<SerializationFormat> formats)
        {
            if (forms == null)
            {
                return;
            }

            foreach (TDForm form in forms)
            {
                AddFormatFromContentType(form.ContentType, formats);
                AddFormatsFromSchemaReferences(form.AdditionalResponses, formats);
                AddFormatsFromSchemaReferences(form.HeaderInfo, formats);
            }
        }

        private static void AddFormatsFromLinks(IEnumerable<TDLink>? links, HashSet<SerializationFormat> formats)
        {
            if (links != null)
            {
                foreach (TDLink link in links)
                {
                    AddFormatFromContentType(link.ContentType, formats);
                }
            }
        }

        private static void AddFormatsFromSchemaReferences(IEnumerable<TDSchemaReference>? schemaRefs, HashSet<SerializationFormat> formats)
        {
            if (schemaRefs != null)
            {
                foreach (TDSchemaReference resp in schemaRefs)
                {
                    AddFormatFromContentType(resp.ContentType, formats);
                }
            }
        }

        private static void AddFormatFromContentType(string? contentType, HashSet<SerializationFormat> formats)
        {
            if (contentType != null)
            {
                formats.Add(ContentTypeToFormat(contentType));
            }
        }
    }
}
