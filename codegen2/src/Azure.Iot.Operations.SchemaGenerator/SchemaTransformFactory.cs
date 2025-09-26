namespace Azure.Iot.Operations.SchemaGenerator
{
    using System;
    using Azure.Iot.Operations.CodeGeneration;

    internal static class SchemaTransformFactory
    {
        internal static ISchemaTemplateTransform GetSchemaTransform(string schemaName, SchemaSpec schemaSpec, string genNamespace)
        {
            return schemaSpec switch
            {
                ObjectSpec objectSpec => GetObjectSchemaTransform(schemaName, objectSpec, genNamespace),
                EnumSpec enumSpec => GetEnumSchemaTransform(schemaName, enumSpec, genNamespace),
                _ => throw new NotSupportedException($"Unable to transform schema spec of type {schemaSpec.GetType()}."),
            };
        }

        internal static ISchemaTemplateTransform GetObjectSchemaTransform(string schemaName, ObjectSpec objectSpec, string genNamespace)
        {
            return objectSpec.Format switch
            {
                SerializationFormat.Json => new ObjectJsonSchema(schemaName, objectSpec, genNamespace),
                _ => throw new NotSupportedException($"Serialization format {objectSpec.Format} is not supported."),
            };
        }

        internal static ISchemaTemplateTransform GetEnumSchemaTransform(string schemaName, EnumSpec enumSpec, string genNamespace)
        {
            return enumSpec.Format switch
            {
                SerializationFormat.Json => new EnumJsonSchema(schemaName, enumSpec, genNamespace),
                _ => throw new NotSupportedException($"Serialization format {enumSpec.Format} is not supported."),
            };
        }
    }
}
