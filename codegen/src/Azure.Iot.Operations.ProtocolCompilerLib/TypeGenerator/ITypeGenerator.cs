namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public interface ITypeGenerator
    {
        void GenerateTypeFromSchema(Action<string, string, string> acceptor, string projectName, SchemaType schemaType, SerializationFormat serFormat);
    }
}
