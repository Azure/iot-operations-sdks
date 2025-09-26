namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    public static class SchemaGenerator
    {
        public static List<GeneratedSchema> GenerateSchemas(TDThing tdThing, SchemaNamer schemaNamer, string projectName, string genNamespace)
        {
            Dictionary<string, SchemaSpec> schemaSpecs = new();
            Dictionary<string, HashSet<SerializationFormat>> referencedSchemas = new();

            PropertySchemaGenerator.GeneratePropertySchemas(tdThing, schemaNamer, projectName, schemaSpecs, referencedSchemas);
            ActionSchemaGenerator.GenerateActionSchemas(tdThing, schemaNamer, projectName, schemaSpecs, referencedSchemas);
            EventSchemaGenerator.GenerateEventSchemas(tdThing, schemaNamer, projectName, schemaSpecs, referencedSchemas);

            Dictionary<string, SchemaSpec> closedSchemaSpecs = new();

            foreach (KeyValuePair<string, HashSet<SerializationFormat>> referencedSchema in referencedSchemas)
            {
                foreach (SerializationFormat format in referencedSchema.Value)
                {
                    if (tdThing.SchemaDefinitions?.TryGetValue(referencedSchema.Key, out TDDataSchema? dataSchema) ?? false)
                    {
                        ComputeClosureOfDataSchema(schemaNamer, referencedSchema.Key, dataSchema, format, closedSchemaSpecs);
                    }
                }
            }

            foreach (KeyValuePair<string, SchemaSpec> schemaSpec in schemaSpecs)
            {
                ComputeClosureOfSchemaSpec(schemaNamer, schemaSpec.Key, schemaSpec.Value, closedSchemaSpecs);
            }

            List<GeneratedSchema> generatedSchemas = new();

            foreach (KeyValuePair<string, SchemaSpec> schemaSpec in closedSchemaSpecs)
            {
                ISchemaTemplateTransform schema = SchemaTransformFactory.GetSchemaTransform(schemaSpec.Key, schemaSpec.Value, genNamespace);
                generatedSchemas.Add(new GeneratedSchema(schema.TransformText(), schema.FileName, schema.FolderPath));
            }

            if (tdThing.SchemaDefinitions?.Any(d => d.Value.Type == TDValues.TypeInteger && d.Value.Const != null) ?? false)
            {
                ISchemaTemplateTransform schema = new ConstSchema(projectName, tdThing.SchemaDefinitions, genNamespace);
                generatedSchemas.Add(new GeneratedSchema(schema.TransformText(), schema.FileName, schema.FolderPath));
            }

            return generatedSchemas;
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

            string schemaName = dataSchema.Title ?? backupName;

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
