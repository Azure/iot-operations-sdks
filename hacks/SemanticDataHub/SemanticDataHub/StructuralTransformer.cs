namespace SemanticDataHub
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Newtonsoft.Json.Linq;

    internal class StructuralTransformer : IDataTransformer
    {
        private Dictionary<string, IDataTransformer> propertyTransformers;

        public StructuralTransformer(string propertyPath, JsonElement elt, string bindingFileName)
        {
            propertyTransformers = new Dictionary<string, IDataTransformer>();

            foreach (JsonProperty prop in elt.EnumerateObject())
            {
                propertyTransformers[prop.Name] = DataTransformerFactory.Create($"{propertyPath}.{prop.Name}", prop.Value, bindingFileName);
            }
        }

        public JToken? TransformData(JToken data)
        {
            JObject obj = new ();

            foreach (KeyValuePair<string, IDataTransformer> transformer in propertyTransformers)
            {
                JToken? result = transformer.Value.TransformData(data);
                if (result != null)
                {
                    obj.Add(transformer.Key, result);
                }
            }

            return obj.Count > 0 ? obj : null;
        }
    }
}
