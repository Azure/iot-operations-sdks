namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    public static class SchemaGenerator
    {
        public static Dictionary<SerializationFormat, List<GeneratedItem>> GenerateSchemas(List<ParsedThing> parsedThings, string projectName, DirectoryInfo workingDir)
        {
            Dictionary<string, ISchemaTemplateTransform> transforms = new();

            foreach (ParsedThing parsedThing in parsedThings)
            {
                Dictionary<string, SchemaSpec> schemaSpecs = new();
                Dictionary<string, HashSet<SerializationFormat>> referencedSchemas = new();

                PropertySchemaGenerator.GeneratePropertySchemas(parsedThing.ErrorReporter, parsedThing.Thing, parsedThing.DirectoryName, parsedThing.SchemaNamer, projectName, schemaSpecs, referencedSchemas);
                ActionSchemaGenerator.GenerateActionSchemas(parsedThing.ErrorReporter, parsedThing.Thing, parsedThing.DirectoryName, parsedThing.SchemaNamer, projectName, schemaSpecs, referencedSchemas);
                EventSchemaGenerator.GenerateEventSchemas(parsedThing.ErrorReporter, parsedThing.Thing, parsedThing.DirectoryName, parsedThing.SchemaNamer, projectName, schemaSpecs, referencedSchemas);

                Dictionary<string, SchemaSpec> closedSchemaSpecs = ComputeClosedSchemaSpecs(parsedThing.ErrorReporter, parsedThing.Thing, parsedThing.SchemaNamer, schemaSpecs, referencedSchemas);

                SchemaTransformFactory transformFactory = new(parsedThing.SchemaNamer, workingDir);

                foreach (KeyValuePair<string, SchemaSpec> schemaSpec in closedSchemaSpecs)
                {
                    if (transformFactory.TryGetSchemaTransform(schemaSpec.Key, schemaSpec.Value, out ISchemaTemplateTransform? transform))
                    {
                        transforms[transform.FileName] = transform;
                    }
                }
            }

            Dictionary<SerializationFormat, List<GeneratedItem>> generatedSchemas = new();

            foreach (KeyValuePair<string, ISchemaTemplateTransform> transform in transforms)
            {
                if (!generatedSchemas.TryGetValue(transform.Value.Format, out List<GeneratedItem>? schemas))
                {
                    schemas = new();
                    generatedSchemas[transform.Value.Format] = schemas;
                }

                schemas.Add(new GeneratedItem(transform.Value.TransformText(), transform.Key));
            }

            return generatedSchemas;
        }

        private static Dictionary<string, SchemaSpec> ComputeClosedSchemaSpecs(ErrorReporter errorReporter, TDThing thing, SchemaNamer schemaNamer, Dictionary<string, SchemaSpec> schemaSpecs, Dictionary<string, HashSet<SerializationFormat>> referencedSchemas)
        {
            Dictionary<string, SchemaSpec> closedSchemaSpecs = new();

            foreach (KeyValuePair<string, HashSet<SerializationFormat>> referencedSchema in referencedSchemas)
            {
                foreach (SerializationFormat format in referencedSchema.Value)
                {
                    if (thing.SchemaDefinitions?.Entries?.TryGetValue(referencedSchema.Key, out ValueTracker<TDDataSchema>? dataSchema) ?? false)
                    {
                        ComputeClosureOfDataSchema(errorReporter, schemaNamer, referencedSchema.Key, dataSchema, format, closedSchemaSpecs);
                    }
                }
            }

            foreach (KeyValuePair<string, SchemaSpec> schemaSpec in schemaSpecs)
            {
                ComputeClosureOfSchemaSpec(errorReporter, schemaNamer, schemaSpec.Key, schemaSpec.Value, closedSchemaSpecs);
            }

            return closedSchemaSpecs;
        }

        private static void ComputeClosureOfSchemaSpec(ErrorReporter errorReporter, SchemaNamer schemaNamer, string schemaName, SchemaSpec schemaSpec, Dictionary<string, SchemaSpec> closedSchemaSpecs)
        {
            if (IsLocalDuplicate(errorReporter, schemaName, schemaSpec, closedSchemaSpecs))
            {
                return;
            }

            closedSchemaSpecs[schemaName] = schemaSpec;
            errorReporter.RegisterName(schemaName, schemaSpec.TokenIndex);

            if (schemaSpec is ObjectSpec objectSpec)
            {
                foreach (KeyValuePair<string, FieldSpec> field in objectSpec.Fields)
                {
                    ComputeClosureOfDataSchema(errorReporter, schemaNamer, field.Value.BackupSchemaName, field.Value.Schema, schemaSpec.Format, closedSchemaSpecs);
                }
            }
        }

        private static void ComputeClosureOfDataSchema(ErrorReporter errorReporter, SchemaNamer schemaNamer, string backupName, ValueTracker<TDDataSchema> dataSchema, SerializationFormat format, Dictionary<string, SchemaSpec> closedSchemaSpecs)
        {
            if (IsProxy(dataSchema.Value))
            {
                return;
            }

            string schemaName = schemaNamer.ApplyBackupSchemaName(dataSchema.Value.Title?.Value.Value, backupName);

            if (SchemaSpec.TryCreateFromDataSchema(errorReporter, schemaNamer, dataSchema, format, backupName, out SchemaSpec? schemaSpec))
            {
                if (IsLocalDuplicate(errorReporter, schemaName, schemaSpec, closedSchemaSpecs))
                {
                    return;
                }

                closedSchemaSpecs[schemaName] = schemaSpec;
                errorReporter.RegisterName(schemaName, schemaSpec.TokenIndex);
            }

            if (dataSchema.Value.Properties?.Entries != null)
            {
                foreach (KeyValuePair<string, ValueTracker<TDDataSchema>> property in dataSchema.Value.Properties.Entries)
                {
                    ComputeClosureOfDataSchema(errorReporter, schemaNamer, schemaNamer.GetBackupSchemaName(schemaName, property.Key), property.Value, format, closedSchemaSpecs);
                }
            }
            else if (dataSchema.Value.Items?.Value != null)
            {
                ComputeClosureOfDataSchema(errorReporter, schemaNamer, backupName, dataSchema.Value.Items, format, closedSchemaSpecs);
            }
            else if (dataSchema.Value.AdditionalProperties?.Value != null)
            {
                ComputeClosureOfDataSchema(errorReporter, schemaNamer, backupName, dataSchema.Value.AdditionalProperties, format, closedSchemaSpecs);
            }
        }

        private static bool IsProxy(TDDataSchema dataSchema)
        {
            return (dataSchema.Type?.Value.Value == TDValues.TypeObject && (dataSchema.AdditionalProperties == null || dataSchema.AdditionalProperties == null) && dataSchema.Properties == null) ||
                (dataSchema.Type?.Value.Value == TDValues.TypeArray && dataSchema.Items == null);
        }

        private static bool IsLocalDuplicate(ErrorReporter errorReporter, string schemaName, SchemaSpec schemaSpec, Dictionary<string, SchemaSpec> closedSchemaSpecs)
        {
            if (!closedSchemaSpecs.TryGetValue(schemaName, out SchemaSpec? existingSpec))
            {
                return false;
            }

            if (existingSpec.GetType() != schemaSpec.GetType())
            {
                errorReporter.ReportError($"Schema name {schemaName} is duplicated on schema with different type.", schemaSpec.TokenIndex, existingSpec.TokenIndex);
                return false;
            }
            else if (existingSpec is ObjectSpec existingObjectSpec && schemaSpec is ObjectSpec newObjectSpec)
            {
                foreach (KeyValuePair<string, FieldSpec> field in existingObjectSpec.Fields)
                {
                    if (!newObjectSpec.Fields.TryGetValue(field.Key, out FieldSpec? newField))
                    {
                        errorReporter.ReportError($"Schema name {schemaName} is duplicated but schema has field '{field.Key}' not present in other schema.", field.Value.Schema.TokenIndex, newObjectSpec.TokenIndex);
                        return false;
                    }
                }

                foreach (KeyValuePair<string, FieldSpec> field in newObjectSpec.Fields)
                {
                    if (!existingObjectSpec.Fields.TryGetValue(field.Key, out FieldSpec? extantField))
                    {
                        errorReporter.ReportError($"Schema name {schemaName} is duplicated but schema has field '{field.Key}' not present in other schema.", field.Value.Schema.TokenIndex, existingObjectSpec.TokenIndex);
                        return false;
                    }
                }

                foreach (KeyValuePair<string, FieldSpec> field in newObjectSpec.Fields)
                {
                    FieldSpec existingFieldValue = existingObjectSpec.Fields[field.Key];
                    if (!field.Value.Equals(existingFieldValue))
                    {
                        errorReporter.ReportError($"Schema name {schemaName} is duplicated but field '{field.Key}' has different value.", field.Value.Schema.TokenIndex, existingFieldValue.Schema.TokenIndex);
                        return false;
                    }
                }

                return true;
            }
            else if (existingSpec is EnumSpec existingEnumSpec && schemaSpec is EnumSpec newEnumSpec)
            {
                foreach (string value in existingEnumSpec.Values)
                {
                    if (!newEnumSpec.Values.Contains(value))
                    {
                        errorReporter.ReportError($"Schema name {schemaName} is duplicated but schema has enum value '{value}' not present in other schema.", existingEnumSpec.TokenIndex, newEnumSpec.TokenIndex);
                        return false;
                    }
                }

                foreach (string value in newEnumSpec.Values)
                {
                    if (!existingEnumSpec.Values.Contains(value))
                    {
                        errorReporter.ReportError($"Schema name {schemaName} is duplicated but schema has enum value '{value}' not present in other schema.", newEnumSpec.TokenIndex, existingEnumSpec.TokenIndex);
                        return false;
                    }
                }

                return true;
            }

            return false;
        }
    }
}
