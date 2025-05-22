namespace SemanticDataHub
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Newtonsoft.Json.Linq;

    internal class MappingTransformer : IDataTransformer
    {
        private SelectionTransformer selectionTransformer;
        private Dictionary<string, JValue> transformationMap;

        public MappingTransformer(JsonElement elt1, JsonElement elt2, string bindingFileName)
        {
            if (elt2.ValueKind != JsonValueKind.Object)
            {
                throw new Exception($"Invalid '@map' definition in binding {bindingFileName}: third array element must be an object");
            }

            selectionTransformer = new SelectionTransformer(elt1, bindingFileName);
            transformationMap = new Dictionary<string, JValue>();

            foreach (JsonProperty prop in elt2.EnumerateObject())
            {
                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.String:
                        transformationMap[prop.Name] = new JValue(prop.Value.GetString());
                        break;
                    case JsonValueKind.Number:
                        transformationMap[prop.Name] = new JValue(prop.Value.GetInt32());
                        break;
                    default:
                        throw new Exception($"Invalid structure in binding {bindingFileName}: mapping object must have string or numeric values");
                }
            }
        }

        public JToken? TransformData(JToken data)
        {
            JToken? selectedToken = selectionTransformer.TransformData(data);
            if (selectedToken == null || selectedToken.Type != JTokenType.String && selectedToken.Type != JTokenType.Integer)
            {
                return null;
            }

            return transformationMap.TryGetValue(((JValue)selectedToken).Value<string>()!, out JValue? value) ? value : null;
        }
    }
}
