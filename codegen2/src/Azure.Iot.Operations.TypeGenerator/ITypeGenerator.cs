namespace Azure.Iot.Operations.TypeGenerator
{
    using System;
    using Azure.Iot.Operations.CodeGeneration;

    internal interface ITypeGenerator
    {
        GeneratedItem GenerateTypeFromSchema(SchemaType schemaType, string projectName, CodeName genNamespace, SerializationFormat serFormat, string srcSubdir);
    }
}
