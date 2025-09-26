namespace Azure.Iot.Operations.TypeGenerator
{
    using System;
    using Azure.Iot.Operations.Serialization;

    public interface ITypeGenerator
    {
        void GenerateTypeFromSchema(Action<string, string, string> acceptor, string projectName, SchemaType schemaType, SerializationFormat serFormat);
    }
}
