// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class PropertySchemaGenerator
    {
        internal static void GeneratePropertySchemas(ErrorReporter errorReporter, TDThing tdThing, string dirName, SchemaNamer schemaNamer, string projectName, Dictionary<string, List<SchemaSpec>> schemaSpecs, Dictionary<string, HashSet<SerializationFormat>> referencedSchemas)
        {
            FormInfo? readAllPropsForm = FormInfo.CreateFromForm(errorReporter, tdThing.Forms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements?.Any(e => e.Value.Value == TDValues.OpReadAllProps) ?? false)?.Value, tdThing.SchemaDefinitions?.Entries);
            FormInfo? writeMultPropsForm = FormInfo.CreateFromForm(errorReporter, tdThing.Forms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements?.Any(e => e.Value.Value == TDValues.OpWriteMultProps) ?? false)?.Value, tdThing.SchemaDefinitions?.Entries);

            Dictionary<string, FieldSpec> readValueFields = new();
            Dictionary<string, FieldSpec> writeValueFields = new();
            Dictionary<string, FieldSpec> readErrorFields = new();
            Dictionary<string, FieldSpec> writeErrorFields = new();
            HashSet<string> readErrorSchemaNames = new();
            HashSet<string> writeErrorSchemaNames = new();

            foreach (KeyValuePair<string, ValueTracker<TDProperty>> propKvp in tdThing.Properties?.Entries ?? new())
            {
                ValueTracker<TDProperty> property = propKvp.Value;
                if (property != null)
                {
                    ProcessProperty(
                        errorReporter,
                        schemaNamer,
                        propKvp.Key,
                        property,
                        projectName,
                        dirName,
                        tdThing.SchemaDefinitions?.Entries,
                        schemaSpecs,
                        readValueFields,
                        readErrorFields,
                        referencedSchemas,
                        readErrorSchemaNames,
                        isRead: true);

                    ProcessProperty(
                        errorReporter,
                        schemaNamer,
                        propKvp.Key,
                        property,
                        projectName,
                        dirName,
                        tdThing.SchemaDefinitions?.Entries,
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
            ErrorReporter errorReporter,
            SchemaNamer schemaNamer,
            string propName,
            ValueTracker<TDProperty> tdProperty,
            string projectName,
            string dirName,
            Dictionary<string, ValueTracker<TDDataSchema>>? schemaDefinitions,
            Dictionary<string, List<SchemaSpec>> schemaSpecs,
            Dictionary<string, FieldSpec> valueFields,
            Dictionary<string, FieldSpec> errorFields,
            Dictionary<string, HashSet<SerializationFormat>> referencedSchemas,
            HashSet<string> errorSchemaNames,
            bool isRead)
        {
            if ((tdProperty.Value.ReadOnly?.Value.Value ?? false) && !isRead)
            {
                return;
            }

            string operation = isRead ? TDValues.OpReadProp : TDValues.OpWriteProp;
            FormInfo? propForm = FormInfo.CreateFromForm(errorReporter, tdProperty.Value.Forms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements?.Any(e => e.Value.Value == operation) ?? false)?.Value, schemaDefinitions);
            propForm ??= FormInfo.CreateFromForm(errorReporter, tdProperty.Value.Forms?.Elements?.FirstOrDefault(f => f.Value.Op == null)?.Value, schemaDefinitions);

            FieldSpec propFieldSpec = new(
                tdProperty.Value.Description?.Value.Value ?? (isRead ? $"The '{propName}' Property value." : $"Value for the '{propName}' Property."),
                new ValueTracker<TDDataSchema> { PropertyName = string.Empty, Value = tdProperty.Value as TDDataSchema, TokenIndex = tdProperty.TokenIndex },
                BackupSchemaName: schemaNamer.GetPropValueSchema(propName),
                Require: isRead,
                Base: dirName,
                Fragment: tdProperty.Value.Placeholder?.Value.Value ?? false);
            valueFields[propName] = propFieldSpec;

            if (propForm?.TopicPattern != null && (isRead || (tdProperty.Value.Placeholder?.Value.Value ?? false)))
            {
                string propSchemaName = isRead ? schemaNamer.GetPropSchema(propName) : schemaNamer.GetWritablePropSchema(propName);
                ObjectSpec propObjectSpec = new(
                    tdProperty.Value.Description?.Value.Value ?? $"Container for{(isRead ? "" : " writing to")} the '{propName}' Property.",
                    new Dictionary<string, FieldSpec> { { propName, propFieldSpec } },
                    propForm.Format,
                    propSchemaName,
                    TokenIndex: -1);

                if (!schemaSpecs.TryGetValue(propSchemaName, out List<SchemaSpec>? propSpecs))
                {
                    propSpecs = new List<SchemaSpec>();
                    schemaSpecs[propSchemaName] = propSpecs;
                }
                propSpecs.Add(propObjectSpec);
            }

            if (propForm?.ErrorRespSchema != null)
            {
                FieldSpec respFieldSpec = new(
                    tdProperty.Value.Description?.Value.Value ?? $"{(isRead ? "Read" : "Write")} error for the '{propName}' Property.",
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
                        tdProperty.Value.Description?.Value.Value ?? $"Response to a '{propName}' Property {(isRead ? "read" : "write")}.",
                        responseFields,
                        propForm.Format,
                        respSchemaName,
                        TokenIndex: -1);

                    if (!schemaSpecs.TryGetValue(respSchemaName, out List<SchemaSpec>? respSpecs))
                    {
                        respSpecs = new List<SchemaSpec>();
                        schemaSpecs[respSchemaName] = respSpecs;
                    }
                    respSpecs.Add(respObjectSpec);

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
            Dictionary<string, List<SchemaSpec>> schemaSpecs,
            Dictionary<string, HashSet<SerializationFormat>> referencedSchemas,
            bool responseIncludesProps)
        {
            if (topLevelPropsForm?.TopicPattern != null)
            {
                if (valueFields.Any())
                {
                    if (!schemaSpecs.TryGetValue(propsSchema, out List<SchemaSpec>? propSpecs))
                    {
                        propSpecs = new List<SchemaSpec>();
                        schemaSpecs[propsSchema] = propSpecs;
                    }
                    propSpecs.Add(new ObjectSpec(
                        $"Values {preposition} {quantifier} Properties.",
                        valueFields,
                        topLevelPropsForm.Format,
                        propsSchema,
                        TokenIndex: -1));
                }

                if (topLevelPropsForm.HasErrorResponse)
                {
                    if (!schemaSpecs.TryGetValue(errorSchema, out List<SchemaSpec>? errorSpecs))
                    {
                        errorSpecs = new List<SchemaSpec>();
                        schemaSpecs[errorSchema] = errorSpecs;
                    }
                    errorSpecs.Add(new ObjectSpec(
                        $"Errors from any Property {operation}.",
                        errorFields,
                        topLevelPropsForm.Format,
                        errorSchema,
                        TokenIndex: -1));

                    Dictionary<string, (string, string)> fieldSketches = new();
                    fieldSketches[schemaNamer.AggregateRespErrorField] = (errorSchema, "Errors when operation fails.");
                    if (responseIncludesProps)
                    {
                        fieldSketches[schemaNamer.AggregateReadRespValueField] = (propsSchema, "Properties when operation succeeds.");
                    }

                    if (!schemaSpecs.TryGetValue(responseSchema, out List<SchemaSpec>? responseSpecs))
                    {
                        responseSpecs = new List<SchemaSpec>();
                        schemaSpecs[responseSchema] = responseSpecs;
                    }
                    responseSpecs.Add(ObjectSpec.CreateFixed(
                        schemaNamer,
                        $"Response to {operation} of {quantifier} Properties",
                        fieldSketches,
                        topLevelPropsForm.Format,
                        responseSchema));

                    foreach (string errSchemaName in errorSchemaNames)
                    {
                        SchemaGenerationSupport.AddSchemaReference(errSchemaName, topLevelPropsForm.Format, referencedSchemas);
                    }
                }
            }
        }
    }
}
