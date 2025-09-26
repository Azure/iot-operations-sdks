namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class PropertySchemaGenerator
    {
        internal static void GeneratePropertySchemas(TDThing tdThing, SchemaNamer schemaNamer, string projectName, Dictionary<string, SchemaSpec> schemaSpecs, Dictionary<string, HashSet<SerializationFormat>> referencedSchemas)
        {
            FormInfo? readAllPropsForm = FormInfo.CreateFromForm(tdThing.Forms?.FirstOrDefault(f => f.Op == TDValues.OpReadAllProps), tdThing.SchemaDefinitions);
            FormInfo? writeMultPropsForm = FormInfo.CreateFromForm(tdThing.Forms?.FirstOrDefault(f => f.Op == TDValues.OpWriteMultProps), tdThing.SchemaDefinitions);

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
                        tdThing.SchemaDefinitions,
                        schemaSpecs,
                        readValueFields,
                        writeValueFields,
                        readErrorFields,
                        writeErrorFields,
                        referencedSchemas,
                        readErrorSchemaNames,
                        writeErrorSchemaNames);
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
            Dictionary<string, TDDataSchema>? schemaDefinitions,
            Dictionary<string, SchemaSpec> schemaSpecs,
            Dictionary<string, FieldSpec> readValueFields,
            Dictionary<string, FieldSpec> writeValueFields,
            Dictionary<string, FieldSpec> readErrorFields,
            Dictionary<string, FieldSpec> writeErrorFields,
            Dictionary<string, HashSet<SerializationFormat>> referencedSchemas,
            HashSet<string> readErrorSchemaNames,
            HashSet<string> writeErrorSchemaNames)
        {
            FormInfo? readPropForm = FormInfo.CreateFromForm(tdProperty.Forms?.FirstOrDefault(f => f.Op == TDValues.OpReadProp), schemaDefinitions);
            FormInfo? writePropForm = FormInfo.CreateFromForm(tdProperty.Forms?.FirstOrDefault(f => f.Op == TDValues.OpWriteProp), schemaDefinitions);

            FieldSpec propFieldSpec = new(
                tdProperty.Description ?? $"The '{propName}' Property value.",
                tdProperty as TDDataSchema,
                BackupSchemaName: schemaNamer.GetPropValueSchema(propName),
                Require: true,
                Fragment: tdProperty.Placeholder);
            readValueFields[propName] = propFieldSpec;

            if (readPropForm?.TopicPattern != null)
            {
                string propSchemaName = schemaNamer.GetPropSchema(propName);
                ObjectSpec propObjectSpec = new(
                    tdProperty.Description ?? $"Container for the '{propName}' Property.",
                    new Dictionary<string, FieldSpec> { { propName, propFieldSpec } },
                    readPropForm.Format,
                    propSchemaName);
                schemaSpecs[propSchemaName] = propObjectSpec;
            }

            if (readPropForm?.ErrorSchema != null)
            {
                FieldSpec propReadRespFieldSpec = new(
                    tdProperty.Description ?? $"Read error for the '{propName}' Property.",
                    readPropForm.ErrorSchema,
                    BackupSchemaName: readPropForm.ErrorSchemaName!,
                    Require: false);
                readErrorFields[propName] = propReadRespFieldSpec;

                readErrorSchemaNames.Add(readPropForm.ErrorSchemaName!);

                if (readPropForm?.TopicPattern != null)
                {
                    string propReadRespSchemaName = schemaNamer.GetPropReadRespSchema(propName);
                    ObjectSpec propReadRespObjectSpec = new(
                        tdProperty.Description ?? $"Response to a '{propName}' Property read.",
                        new Dictionary<string, FieldSpec> { { propName, propFieldSpec with { Require = false } }, { schemaNamer.PropRespErrorField, propReadRespFieldSpec } },
                        readPropForm.Format,
                        propReadRespSchemaName);
                    schemaSpecs[propReadRespSchemaName] = propReadRespObjectSpec;

                    SchemaGenerationSupport.AddSchemaReference(readPropForm.ErrorSchemaName!, readPropForm.ErrorSchemaFormat, referencedSchemas);
                }
            }

            if (!tdProperty.ReadOnly)
            {
                FieldSpec writablePropFieldSpec = new(
                    tdProperty.Description ?? $"Value for the '{propName}' Property.",
                    tdProperty as TDDataSchema,
                    BackupSchemaName: schemaNamer.GetPropValueSchema(propName),
                    Require: false,
                    Fragment: tdProperty.Placeholder);
                writeValueFields[propName] = writablePropFieldSpec;

                if (tdProperty.Placeholder && writePropForm?.TopicPattern != null)
                {
                    string writablePropSchemaName = schemaNamer.GetWritablePropSchema(propName);
                    ObjectSpec writablePropObjectSpec = new(
                        tdProperty.Description ?? $"Container for writing to the '{propName}' Property.",
                        new Dictionary<string, FieldSpec> { { propName, writablePropFieldSpec } },
                        writePropForm.Format,
                        writablePropSchemaName);
                    schemaSpecs[writablePropSchemaName] = writablePropObjectSpec;
                }

                if (writePropForm?.ErrorSchema != null)
                {
                    FieldSpec propWriteRespFieldSpec = new(
                        tdProperty.Description ?? $"Write error for the '{propName}' Property.",
                        writePropForm.ErrorSchema,
                        BackupSchemaName: writePropForm.ErrorSchemaName!,
                        Require: false);
                    writeErrorFields[propName] = propWriteRespFieldSpec;

                    writeErrorSchemaNames.Add(writePropForm.ErrorSchemaName!);

                    if (writePropForm?.TopicPattern != null)
                    {
                        string propWriteRespSchemaName = schemaNamer.GetPropWriteRespSchema(propName);
                        ObjectSpec propWriteRespObjectSpec = new(
                            tdProperty.Description ?? $"Response to a '{propName}' Property write.",
                            new Dictionary<string, FieldSpec> { { schemaNamer.PropRespErrorField, propWriteRespFieldSpec } },
                            writePropForm.Format,
                            propWriteRespSchemaName);
                        schemaSpecs[propWriteRespSchemaName] = propWriteRespObjectSpec;

                        SchemaGenerationSupport.AddSchemaReference(writePropForm.ErrorSchemaName!, writePropForm.ErrorSchemaFormat, referencedSchemas);
                    }
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

                if (topLevelPropsForm.HasErrorResponse && errorFields.Any())
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
