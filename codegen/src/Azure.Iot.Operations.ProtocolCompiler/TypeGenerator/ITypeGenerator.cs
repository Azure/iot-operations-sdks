namespace Azure.Iot.Operations.ProtocolCompiler
{
    public interface ITypeGenerator
    {
        void GenerateTypeFromSchema(string projectName, CodeName genNamespace, SchemaType schemaType, SerializationFormat serFormat, string outputFolder);
    }
}
