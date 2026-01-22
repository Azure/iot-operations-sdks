// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    internal interface ITypeGenerator
    {
        TargetLanguage TargetLanguage { get; }

        GeneratedItem GenerateTypeFromSchema(SchemaType schemaType, string projectName, CodeName genNamespace, SerializationFormat serFormat, string srcSubdir);
    }
}
