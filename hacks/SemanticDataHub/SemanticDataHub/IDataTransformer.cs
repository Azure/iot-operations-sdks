namespace SemanticDataHub
{
    using Newtonsoft.Json.Linq;

    internal interface IDataTransformer
    {
        public JToken? TransformData(JToken data);
    }
}
