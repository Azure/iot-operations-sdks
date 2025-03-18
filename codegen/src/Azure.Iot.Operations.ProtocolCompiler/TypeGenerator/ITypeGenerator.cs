namespace Azure.Iot.Operations.ProtocolCompiler
{
    public interface ITypeGenerator
    {
        void GenerateTypeFromSchema(string projectName, SchemaType schemaType, SerializationFormat serFormat, string outputFolder);
    }
}
