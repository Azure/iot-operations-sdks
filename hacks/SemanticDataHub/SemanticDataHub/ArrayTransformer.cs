namespace SemanticDataHub
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Newtonsoft.Json.Linq;

    internal class ArrayTransformer : IDataTransformer
    {
        private List<IDataTransformer> propertyTransformers;

        public ArrayTransformer(string propertyPath, JsonElement elt, string bindingFileName)
        {
            propertyTransformers = new List<IDataTransformer>();

            foreach (JsonElement subElt in elt.EnumerateArray())
            {
                propertyTransformers.Add(DataTransformerFactory.Create(propertyPath, subElt, bindingFileName));
            }
        }

        public JToken? TransformData(JToken data)
        {
            JArray array = new ();

            foreach (IDataTransformer transformer in propertyTransformers)
            {
                JToken? result = transformer.TransformData(data);
                if (result != null)
                {
                    array.Add(result);
                }
            }

            return array.Count > 0 ? array : null;
        }
    }
}
