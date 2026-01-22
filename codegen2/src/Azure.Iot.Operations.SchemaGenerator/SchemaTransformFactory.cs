// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    internal class SchemaTransformFactory
    {
        private readonly JsonSchemaSupport jsonSchemaSupport;

        internal SchemaTransformFactory(SchemaNamer schemaNamer, DirectoryInfo workingDir)
        {
            this.jsonSchemaSupport = new JsonSchemaSupport(schemaNamer, workingDir);
        }

        internal bool TryGetSchemaTransform(string schemaName, SchemaSpec schemaSpec, [NotNullWhen(true)] out ISchemaTemplateTransform? transform)
        {
            switch (schemaSpec)
            {
                case ObjectSpec objectSpec:
                    return TryGetObjectSchemaTransform(schemaName, objectSpec, out transform);
                case EnumSpec enumSpec:
                    return TryGetEnumSchemaTransform(schemaName, enumSpec, out transform);
                case AliasSpec aliasSpec:
                    return GetAliasSchemaTransform(schemaName, aliasSpec, out transform);
                default:
                    transform = null;
                    return false;
            }
        }

        internal bool TryGetObjectSchemaTransform(string schemaName, ObjectSpec objectSpec, [NotNullWhen(true)] out ISchemaTemplateTransform? transform)
        {
            switch (objectSpec.Format)
            {
                case SerializationFormat.Json:
                    transform = new ObjectJsonSchema(this.jsonSchemaSupport, schemaName, objectSpec);
                    return true;
                default:
                    transform = null;
                    return false;
            }
        }

        internal bool TryGetEnumSchemaTransform(string schemaName, EnumSpec enumSpec, [NotNullWhen(true)] out ISchemaTemplateTransform? transform)
        {
            switch (enumSpec.Format)
            {
                case SerializationFormat.Json:
                    transform = new EnumJsonSchema(schemaName, enumSpec);
                    return true;
                default:
                    transform = null;
                    return false;
            }
        }

        internal bool GetAliasSchemaTransform(string schemaName, AliasSpec aliasSpec, [NotNullWhen(true)] out ISchemaTemplateTransform? transform)
        {
            switch (aliasSpec.Format)
            {
                case SerializationFormat.Json:
                    transform = new AliasJsonSchema(this.jsonSchemaSupport, schemaName, aliasSpec);
                    return true;
                default:
                    transform = null;
                    return false;
            }
        }
    }
}
