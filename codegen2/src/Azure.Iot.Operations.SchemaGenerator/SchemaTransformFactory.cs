namespace Azure.Iot.Operations.SchemaGenerator
{
    using System;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;

    internal class SchemaTransformFactory
    {
        private readonly JsonSchemaSupport jsonSchemaSupport;

        internal SchemaTransformFactory(SchemaNamer schemaNamer, DirectoryInfo workingDir)
        {
            this.jsonSchemaSupport = new JsonSchemaSupport(schemaNamer, workingDir);
        }

        internal ISchemaTemplateTransform GetSchemaTransform(string schemaName, SchemaSpec schemaSpec)
        {
            return schemaSpec switch
            {
                ObjectSpec objectSpec => GetObjectSchemaTransform(schemaName, objectSpec),
                EnumSpec enumSpec => GetEnumSchemaTransform(schemaName, enumSpec),
                AliasSpec aliasSpec => GetAliasSchemaTransform(schemaName, aliasSpec),
                _ => throw new NotSupportedException($"Unable to transform schema spec of type {schemaSpec.GetType()}."),
            };
        }

        internal ISchemaTemplateTransform GetObjectSchemaTransform(string schemaName, ObjectSpec objectSpec)
        {
            return objectSpec.Format switch
            {
                SerializationFormat.Json => new ObjectJsonSchema(this.jsonSchemaSupport, schemaName, objectSpec),
                _ => throw new NotSupportedException($"Serialization format {objectSpec.Format} is not supported."),
            };
        }

        internal ISchemaTemplateTransform GetEnumSchemaTransform(string schemaName, EnumSpec enumSpec)
        {
            return enumSpec.Format switch
            {
                SerializationFormat.Json => new EnumJsonSchema(schemaName, enumSpec),
                _ => throw new NotSupportedException($"Serialization format {enumSpec.Format} is not supported."),
            };
        }

        internal ISchemaTemplateTransform GetAliasSchemaTransform(string schemaName, AliasSpec aliasSpec)
        {
            return aliasSpec.Format switch
            {
                SerializationFormat.Json => new AliasJsonSchema(this.jsonSchemaSupport, schemaName, aliasSpec),
                _ => throw new NotSupportedException($"Serialization format {aliasSpec.Format} is not supported."),
            };
        }
    }
}
