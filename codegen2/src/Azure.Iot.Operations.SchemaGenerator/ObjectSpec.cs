// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Linq;
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    internal record ObjectSpec(string? Description, Dictionary<string, FieldSpec> Fields, SerializationFormat Format, string SchemaName, long TokenIndex) : SchemaSpec(Format, TokenIndex)
    {
        internal static ObjectSpec CreateFromDataSchema(ErrorReporter errorReporter, SchemaNamer schemaNamer, ValueTracker<TDDataSchema> dataSchema, SerializationFormat format, string backupName, string? defaultDescription = null)
        {
            string schemaName = schemaNamer.ApplyBackupSchemaName(dataSchema.Value.Title?.Value.Value, backupName);

            if (dataSchema.Value.Type?.Value.Value != TDValues.TypeObject)
            {
                errorReporter.ReportError(ErrorCondition.TypeMismatch, $"Object schema '{schemaName}' must have type 'object'.", dataSchema.TokenIndex);
            }

            Dictionary<string, FieldSpec> fieldSpecs = new();
            foreach (KeyValuePair<string, ValueTracker<TDDataSchema>> property in dataSchema.Value.Properties?.Entries ?? new Dictionary<string, ValueTracker<TDDataSchema>>())
            {
                fieldSpecs[property.Key] = new FieldSpec(property.Value.Value.Description?.Value.Value ?? $"The '{property.Key}' Field.", property.Value, Require: dataSchema.Value.Required?.Elements?.Any(e => e.Value.Value == property.Key) ?? false, schemaNamer.GetBackupSchemaName(schemaName, property.Key), string.Empty);
            }

            string? description = dataSchema.Value.Description?.Value.Value ?? defaultDescription;

            return new ObjectSpec(description, fieldSpecs, format, schemaName, dataSchema.TokenIndex);
        }

        internal static ObjectSpec CreateFixed(SchemaNamer schemaNamer, string description, Dictionary<string, (string, string)> fieldSketches, SerializationFormat format, string schemaName)
        {
            return new ObjectSpec(
                description,
                fieldSketches.ToDictionary(f => f.Key, f => FieldSpec.CreateFixed(f.Value.Item1, f.Value.Item2, schemaNamer.GetBackupSchemaName(schemaName, f.Value.Item1))),
                format,
                schemaName,
                TokenIndex: -1);
        }
    }
}
