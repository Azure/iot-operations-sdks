namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Collections.Generic;
    using System.IO;
    using Azure.Iot.Operations.CodeGeneration;
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

                PropertySchemaGenerator.GeneratePropertySchemas(parsedThing.Thing, parsedThing.DirectoryName, parsedThing.SchemaNamer, projectName, schemaSpecs, referencedSchemas);
                ActionSchemaGenerator.GenerateActionSchemas(parsedThing.Thing, parsedThing.SchemaNamer, projectName, schemaSpecs, referencedSchemas);
                EventSchemaGenerator.GenerateEventSchemas(parsedThing.Thing, parsedThing.DirectoryName, parsedThing.SchemaNamer, projectName, schemaSpecs, referencedSchemas);

                Dictionary<string, SchemaSpec> closedSchemaSpecs = ComputeClosedSchemaSpecs(parsedThing.Thing, parsedThing.SchemaNamer, schemaSpecs, referencedSchemas);

                SchemaTransformFactory transformFactory = new(parsedThing.SchemaNamer, workingDir);

                foreach (KeyValuePair<string, SchemaSpec> schemaSpec in closedSchemaSpecs)
                {
                    ISchemaTemplateTransform transform = transformFactory.GetSchemaTransform(schemaSpec.Key, schemaSpec.Value);
                    transforms[transform.FileName] = transform;
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

        private static Dictionary<string, SchemaSpec> ComputeClosedSchemaSpecs(TDThing thing, SchemaNamer schemaNamer, Dictionary<string, SchemaSpec> schemaSpecs, Dictionary<string, HashSet<SerializationFormat>> referencedSchemas)
        {
            Dictionary<string, SchemaSpec> closedSchemaSpecs = new();

            foreach (KeyValuePair<string, HashSet<SerializationFormat>> referencedSchema in referencedSchemas)
            {
                foreach (SerializationFormat format in referencedSchema.Value)
                {
                    if (thing.SchemaDefinitions?.TryGetValue(referencedSchema.Key, out TDDataSchema? dataSchema) ?? false)
                    {
                        ComputeClosureOfDataSchema(schemaNamer, referencedSchema.Key, dataSchema, format, closedSchemaSpecs);
                    }
                }
            }

            foreach (KeyValuePair<string, SchemaSpec> schemaSpec in schemaSpecs)
            {
                ComputeClosureOfSchemaSpec(schemaNamer, schemaSpec.Key, schemaSpec.Value, closedSchemaSpecs);
            }

            return closedSchemaSpecs;
        }

        private static void ComputeClosureOfSchemaSpec(SchemaNamer schemaNamer, string schemaName, SchemaSpec schemaSpec, Dictionary<string, SchemaSpec> closedSchemaSpecs)
        {
            if (IsDuplicate(schemaName, schemaSpec, closedSchemaSpecs))
            {
                return;
            }

            closedSchemaSpecs[schemaName] = schemaSpec;

            if (schemaSpec is ObjectSpec objectSpec)
            {
                foreach (KeyValuePair<string, FieldSpec> field in objectSpec.Fields)
                {
                    ComputeClosureOfDataSchema(schemaNamer, field.Value.BackupSchemaName, field.Value.Schema, schemaSpec.Format, closedSchemaSpecs);
                }
            }
        }

        private static void ComputeClosureOfDataSchema(SchemaNamer schemaNamer, string backupName, TDDataSchema dataSchema, SerializationFormat format, Dictionary<string, SchemaSpec> closedSchemaSpecs)
        {
            if (IsProxy(dataSchema))
            {
                return;
            }

            string schemaName = schemaNamer.ApplyBackupSchemaName(dataSchema.Title, backupName);

            if (SchemaSpec.TryCreateFromDataSchema(schemaNamer, dataSchema, format, backupName, out SchemaSpec? schemaSpec))
            {
                if (IsDuplicate(schemaName, schemaSpec, closedSchemaSpecs))
                {
                    return;
                }

                closedSchemaSpecs[schemaName] = schemaSpec;
            }

            if (dataSchema.Properties != null)
            {
                foreach (KeyValuePair<string, TDDataSchema> property in dataSchema.Properties)
                {
                    ComputeClosureOfDataSchema(schemaNamer, schemaNamer.GetBackupSchemaName(schemaName, property.Key), property.Value, format, closedSchemaSpecs);
                }
            }
            else if (dataSchema.Items != null)
            {
                ComputeClosureOfDataSchema(schemaNamer, backupName, dataSchema.Items, format, closedSchemaSpecs);
            }
            else if (dataSchema.AdditionalProperties?.DataSchema != null)
            {
                ComputeClosureOfDataSchema(schemaNamer, backupName, dataSchema.AdditionalProperties.DataSchema, format, closedSchemaSpecs);
            }
        }

        private static bool IsProxy(TDDataSchema dataSchema)
        {
            return (dataSchema.Type == TDValues.TypeObject && (dataSchema.AdditionalProperties == null || dataSchema.AdditionalProperties.Boolean == false) && dataSchema.Properties == null) ||
                (dataSchema.Type == TDValues.TypeArray && dataSchema.Items == null);
        }

        private static bool IsDuplicate(string schemaName, SchemaSpec schemaSpec, Dictionary<string, SchemaSpec> closedSchemaSpecs)
        {
            if (!closedSchemaSpecs.TryGetValue(schemaName, out SchemaSpec? existingSpec))
            {
                return false;
            }

            if (existingSpec.GetType() != schemaSpec.GetType())
            {
                throw new System.Exception($"Duplicate schema name {schemaName} on different schema types.");
            }
            else if (existingSpec is ObjectSpec existingObjectSpec && schemaSpec is ObjectSpec newObjectSpec)
            {
                if (existingObjectSpec.Fields.Count != newObjectSpec.Fields.Count)
                {
                    throw new System.Exception($"Duplicate schema name {schemaName} on objects with different field counts.");
                }

                foreach (KeyValuePair<string, FieldSpec> field in existingObjectSpec.Fields)
                {
                    if (!newObjectSpec.Fields.TryGetValue(field.Key, out FieldSpec? newField))
                    {
                        throw new System.Exception($"Duplicate schema name {schemaName} on objects with different field names (`{field.Key}` present in only one).");
                    }

                    if (!field.Value.Equals(newField))
                    {
                        throw new System.Exception($"Duplicate schema name {schemaName} on objects with different values for field `{field.Key}`.");
                    }
                }

                return true;
            }
            else if (existingSpec is EnumSpec existingEnumSpec && schemaSpec is EnumSpec newEnumSpec)
            {
                if (existingEnumSpec.Values.Count != newEnumSpec.Values.Count)
                {
                    throw new System.Exception($"Duplicate schema name {schemaName} on enums with different value counts.");
                }

                foreach (string value in existingEnumSpec.Values)
                {
                    if (!newEnumSpec.Values.Contains(value))
                    {
                        throw new System.Exception($"Duplicate schema name {schemaName} on enums with different values (`{value}` present in only one).");
                    }
                }

                return true;
            }

            return false;
        }
    }
}
