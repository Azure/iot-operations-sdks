namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class PropertySchemaGenerator
    {
        internal static void GeneratePropertySchemas(TDThing tdThing, string dirName, SchemaNamer schemaNamer, string projectName, Dictionary<string, SchemaSpec> schemaSpecs, Dictionary<string, HashSet<SerializationFormat>> referencedSchemas)
        {
            FormInfo? readAllPropsForm = FormInfo.CreateFromForm(tdThing.Forms?.FirstOrDefault(f => f.Op?.Values.Contains(TDValues.OpReadAllProps) ?? false), tdThing.SchemaDefinitions);
            FormInfo? writeMultPropsForm = FormInfo.CreateFromForm(tdThing.Forms?.FirstOrDefault(f => f.Op?.Values.Contains(TDValues.OpWriteMultProps) ?? false), tdThing.SchemaDefinitions);

            Dictionary<string, FieldSpec> readValueFields = new();
            Dictionary<string, FieldSpec> writeValueFields = new();
            Dictionary<string, FieldSpec> readErrorFields = new();
            Dictionary<string, FieldSpec> writeErrorFields = new();
            HashSet<string> readErrorSchemaNames = new();
            HashSet<string> writeErrorSchemaNames = new();

            if (tdThing.Properties != null)
            {
                foreach (KeyValuePair<string, TDProperty> propKvp in tdThing.Properties)
                {
                    ProcessProperty(
                        schemaNamer,
                        propKvp.Key,
                        propKvp.Value,
                        projectName,
                        dirName,
                        tdThing.SchemaDefinitions,
                        schemaSpecs,
                        readValueFields,
                        readErrorFields,
                        referencedSchemas,
                        readErrorSchemaNames,
                        isRead: true);

                    ProcessProperty(
                        schemaNamer,
                        propKvp.Key,
                        propKvp.Value,
                        projectName,
                        dirName,
                        tdThing.SchemaDefinitions,
                        schemaSpecs,
                        writeValueFields,
                        writeErrorFields,
                        referencedSchemas,
                        writeErrorSchemaNames,
                        isRead: false);
                }
            }

            GenerateCollectiveResponseObject(
                schemaNamer,
                readAllPropsForm,
                readValueFields,
                readErrorFields,
                schemaNamer.AggregatePropSchema,
                schemaNamer.AggregatePropReadErrSchema,
                schemaNamer.AggregatePropReadRespSchema,
                "read",
                "of",
                "all",
                readErrorSchemaNames,
                schemaSpecs,
                referencedSchemas,
                responseIncludesProps: true);
            GenerateCollectiveResponseObject(
                schemaNamer,
                writeMultPropsForm,
                writeValueFields,
                writeErrorFields,
                schemaNamer.AggregatePropWriteSchema,
                schemaNamer.AggregatePropWriteErrSchema,
                schemaNamer.AggregatePropWriteRespSchema,
                "write",
                "for",
                "multiple",
                writeErrorSchemaNames,
                schemaSpecs,
                referencedSchemas,
                responseIncludesProps: false);
        }

        private static void ProcessProperty(
            SchemaNamer schemaNamer,
            string propName,
            TDProperty tdProperty,
            string projectName,
            string dirName,
            Dictionary<string, TDDataSchema>? schemaDefinitions,
            Dictionary<string, SchemaSpec> schemaSpecs,
            Dictionary<string, FieldSpec> valueFields,
            Dictionary<string, FieldSpec> errorFields,
            Dictionary<string, HashSet<SerializationFormat>> referencedSchemas,
            HashSet<string> errorSchemaNames,
            bool isRead)
        {
            if (tdProperty.ReadOnly && !isRead)
            {
                return;
            }

            string operation = isRead ? TDValues.OpReadProp : TDValues.OpWriteProp;
            FormInfo? propForm = FormInfo.CreateFromForm(tdProperty.Forms?.FirstOrDefault(f => f.Op?.Values.Contains(operation) ?? false), schemaDefinitions);
            propForm ??= FormInfo.CreateFromForm(tdProperty.Forms?.FirstOrDefault(f => f.Op == null), schemaDefinitions);

            FieldSpec propFieldSpec = new(
                tdProperty.Description ?? (isRead ? $"The '{propName}' Property value." : $"Value for the '{propName}' Property."),
                tdProperty as TDDataSchema,
                BackupSchemaName: schemaNamer.GetPropValueSchema(propName),
                Require: isRead,
                Base: dirName,
                Fragment: tdProperty.Placeholder);
            valueFields[propName] = propFieldSpec;

            if (propForm?.TopicPattern != null && (isRead || tdProperty.Placeholder))
            {
                string propSchemaName = isRead ? schemaNamer.GetPropSchema(propName) : schemaNamer.GetWritablePropSchema(propName);
                ObjectSpec propObjectSpec = new(
                    tdProperty.Description ?? $"Container for{(isRead ? "" : " writing to")} the '{propName}' Property.",
                    new Dictionary<string, FieldSpec> { { propName, propFieldSpec } },
                    propForm.Format,
                    propSchemaName);
                schemaSpecs[propSchemaName] = propObjectSpec;
            }

            if (propForm?.ErrorRespSchema != null)
            {
                FieldSpec respFieldSpec = new(
                    tdProperty.Description ?? $"{(isRead ? "Read" : "Write")} error for the '{propName}' Property.",
                    propForm.ErrorRespSchema,
                    BackupSchemaName: propForm.ErrorRespName!,
                    Require: false,
                    Base: dirName);
                errorFields[propName] = respFieldSpec;

                errorSchemaNames.Add(propForm.ErrorRespName!);

                if (propForm?.TopicPattern != null)
                {
                    Dictionary<string, FieldSpec> responseFields = new();
                    if (isRead)
                    {
                        responseFields[propName] = propFieldSpec with { ForceOption = true };
                        responseFields[schemaNamer.GetPropReadRespErrorField(propName, propForm.ErrorRespName!)] = respFieldSpec;
                    }
                    else
                    {
                        responseFields[schemaNamer.GetPropWriteRespErrorField(propName, propForm.ErrorRespName!)] = respFieldSpec;
                    }

                    string respSchemaName = isRead ? schemaNamer.GetPropReadRespSchema(propName) : schemaNamer.GetPropWriteRespSchema(propName);
                    ObjectSpec respObjectSpec = new(
                        tdProperty.Description ?? $"Response to a '{propName}' Property {(isRead ? "read" : "write")}.",
                        responseFields,
                        propForm.Format,
                        respSchemaName);
                    schemaSpecs[respSchemaName] = respObjectSpec;

                    SchemaGenerationSupport.AddSchemaReference(propForm.ErrorRespName!, propForm.ErrorRespFormat, referencedSchemas);
                }
            }
        }

        private static void GenerateCollectiveResponseObject(
            SchemaNamer schemaNamer,
            FormInfo? topLevelPropsForm,
            Dictionary<string, FieldSpec> valueFields,
            Dictionary<string, FieldSpec> errorFields,
            string propsSchema,
            string errorSchema,
            string responseSchema,
            string operation,
            string preposition,
            string quantifier,
            HashSet<string> errorSchemaNames,
            Dictionary<string, SchemaSpec> schemaSpecs,
            Dictionary<string, HashSet<SerializationFormat>> referencedSchemas,
            bool responseIncludesProps)
        {
            if (topLevelPropsForm?.TopicPattern != null)
            {
                if (valueFields.Any())
                {
                    schemaSpecs[propsSchema] = new ObjectSpec(
                        $"Values {preposition} {quantifier} Properties.",
                        valueFields,
                        topLevelPropsForm.Format,
                        propsSchema);
                }

                if (topLevelPropsForm.HasErrorResponse)
                {
                    schemaSpecs[errorSchema] = new ObjectSpec(
                        $"Errors from any Property {operation}.",
                        errorFields,
                        topLevelPropsForm.Format,
                        errorSchema);

                    Dictionary<string, (string, string)> fieldSketches = new();
                    fieldSketches[schemaNamer.AggregateRespErrorField] = (errorSchema, "Errors when operation fails.");
                    if (responseIncludesProps)
                    {
                        fieldSketches[schemaNamer.AggregateReadRespValueField] = (propsSchema, "Properties when operation succeeds.");
                    }

                    schemaSpecs[responseSchema] = ObjectSpec.CreateFixed(
                        schemaNamer,
                        $"Response to {operation} of {quantifier} Properties",
                        fieldSketches,
                        topLevelPropsForm.Format,
                        responseSchema);

                    foreach (string errSchemaName in errorSchemaNames)
                    {
                        SchemaGenerationSupport.AddSchemaReference(errSchemaName, topLevelPropsForm.Format, referencedSchemas);
                    }
                }
            }
        }
    }
}
