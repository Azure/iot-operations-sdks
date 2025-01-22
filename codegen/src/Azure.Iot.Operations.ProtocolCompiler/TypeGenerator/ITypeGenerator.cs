namespace Azure.Iot.Operations.ProtocolCompiler
{
    public interface ITypeGenerator
    {
        void GenerateTypeFromSchema(string projectName, string genNamespace, SchemaType schemaType, SerializationFormat serFormat, string outputFolder, HashSet<string> sourceFilePaths);
    }
}
