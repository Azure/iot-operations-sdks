namespace SemanticDataHub
{
    using System.Text.Json;
    using Newtonsoft.Json.Linq;

    internal class SelectionTransformer : IDataTransformer
    {
        string jsonPathString;

        public SelectionTransformer(JsonElement elt, string bindingFileName)
        {
            jsonPathString = elt.GetString()!;
        }

        public JToken? TransformData(JToken data)
        {
            return data.SelectToken(jsonPathString)!;
        }
    }
}
