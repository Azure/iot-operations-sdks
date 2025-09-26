namespace Azure.Iot.Operations.TypeGenerator
{
    using System;
    using Azure.Iot.Operations.CodeGeneration;

    internal interface ITypeGenerator
    {
        GeneratedType GenerateTypeFromSchema(SchemaType schemaType, string projectName, SerializationFormat serFormat);
    }
}
