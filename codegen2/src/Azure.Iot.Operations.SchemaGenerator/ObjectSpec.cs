namespace Azure.Iot.Operations.SchemaGenerator
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal record ObjectSpec(string? Description, Dictionary<string, FieldSpec> Fields, SerializationFormat Format, string SchemaName) : SchemaSpec(Format)
    {
        internal static ObjectSpec CreateFromDataSchema(SchemaNamer schemaNamer, TDDataSchema dataSchema, SerializationFormat format, string backupName, string? defaultDescription = null)
        {
            string schemaName = schemaNamer.ApplyBackupSchemaName(dataSchema.Title, backupName);

            if (dataSchema.Type != TDValues.TypeObject)
            {
                throw new Exception($"Cannot create object spec from schema definition with type {dataSchema.Type ?? "unspecfied"}.");
            }

            Dictionary<string, FieldSpec> fieldSpecs = new();
            foreach (KeyValuePair<string, TDDataSchema> property in dataSchema.Properties ?? new Dictionary<string, TDDataSchema>())
            {
                fieldSpecs[property.Key] = new FieldSpec(property.Value.Description ?? $"The '{property.Key}' Field.", property.Value, Require: dataSchema.Required?.Contains(property.Key) ?? false, schemaNamer.GetBackupSchemaName(schemaName, property.Key));
            }

            return new ObjectSpec(dataSchema.Description ?? defaultDescription, fieldSpecs, format, schemaName);
        }

        internal static ObjectSpec CreateFixed(SchemaNamer schemaNamer, string description, Dictionary<string, (string, string)> fieldSketches, SerializationFormat format, string schemaName)
        {
            return new ObjectSpec(
                description,
                fieldSketches.ToDictionary(f => f.Key, f => FieldSpec.CreateFixed(f.Value.Item1, f.Value.Item2, schemaNamer.GetBackupSchemaName(schemaName, f.Value.Item1))),
                format,
                schemaName);
        }
    }
}
