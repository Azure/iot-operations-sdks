// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class PropertyEnvoyGenerator
    {
        internal static List<PropertySpec> GeneratePropertyEnvoys(
            ErrorReporter errorReporter,
            TDThing tdThing,
            SchemaNamer schemaNamer,
            CodeName serviceName,
            EnvoyTransformFactory envoyFactory,
            Dictionary<string, IEnvoyTemplateTransform> transforms,
            Dictionary<string, ErrorSpec> errorSpecs,
            Dictionary<string, AggregateErrorSpec> aggErrorSpecs,
            Dictionary<SerializationFormat, HashSet<string>> formattedTypesToSerialize,
            bool generateClient,
            bool generateServer)
        {
            List<PropertySpec> propertySpecs = new();
            Dictionary<string, string> readInnerErrors = new();
            Dictionary<string, string> writeInnerErrors = new();

            foreach (KeyValuePair<string, ValueTracker<TDProperty>> propKvp in tdThing.Properties?.Entries ?? new())
            {
                TDProperty? property = propKvp.Value.Value;
                if (property == null)
                {
                    continue;
                }

                FormInfo? readPropForm = FormInfo.CreateFromForm(errorReporter, property.Forms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements?.Any(e => e.Value.Value == TDValues.OpReadProp) ?? false)?.Value, tdThing.SchemaDefinitions?.Entries);
                FormInfo? writePropForm = FormInfo.CreateFromForm(errorReporter, property.Forms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements?.Any(e => e.Value.Value == TDValues.OpWriteProp) ?? false)?.Value, tdThing.SchemaDefinitions?.Entries);
                FormInfo? noOpForm = FormInfo.CreateFromForm(errorReporter, property.Forms?.Elements?.FirstOrDefault(f => f.Value.Op == null)?.Value, tdThing.SchemaDefinitions?.Entries);
                readPropForm ??= noOpForm;
                writePropForm ??= noOpForm;

                string propSchema = schemaNamer.GetPropSchema(propKvp.Key);
                string? readRespSchema = null;
                string? readErrorSchema = GetAndRecordError(propKvp.Key, readPropForm, schemaNamer, errorSpecs, formattedTypesToSerialize, readInnerErrors);
                if (readPropForm?.TopicPattern != null && readPropForm.Format != SerializationFormat.None)
                {
                    readRespSchema = readPropForm.ErrorRespSchema != null ? schemaNamer.GetPropReadRespSchema(propKvp.Key) : propSchema;
                    formattedTypesToSerialize[readPropForm.Format].Add(readRespSchema);
                    formattedTypesToSerialize[readPropForm.Format].Add(propSchema);
                }

                string? writeReqSchema = null;
                string? writeRespSchema = null;
                string? writeErrorSchema = null;
                if (!property.ReadOnly?.Value.Value ?? false)
                {
                    if (writePropForm?.TopicPattern != null && writePropForm.Format != SerializationFormat.None)
                    {
                        writeReqSchema = (property.Placeholder?.Value.Value ?? false) ? schemaNamer.GetWritablePropSchema(propKvp.Key) : propSchema;
                        formattedTypesToSerialize[writePropForm.Format].Add(writeReqSchema);

                        if (writePropForm.HasErrorResponse)
                        {
                            writeRespSchema = schemaNamer.GetPropWriteRespSchema(propKvp.Key);
                            formattedTypesToSerialize[writePropForm.Format].Add(writeRespSchema);
                        }
                    }

                    writeErrorSchema = GetAndRecordError(propKvp.Key, writePropForm, schemaNamer, errorSpecs, formattedTypesToSerialize, writeInnerErrors);
                }

                if (readRespSchema != null || writeReqSchema != null)
                {
                    SerializationFormat readFormat = readPropForm?.Format ?? SerializationFormat.None;
                    SerializationFormat writeFormat = writePropForm?.Format ?? SerializationFormat.None;

                    string? readErrorName = readErrorSchema != null ? schemaNamer.GetPropReadRespErrorField(propKvp.Key, readErrorSchema) : null;
                    string? writeErrorName = writeErrorSchema != null ? schemaNamer.GetPropWriteRespErrorField(propKvp.Key, writeErrorSchema) : null;

                    string readTopicPattern = readPropForm?.TopicPattern ?? string.Empty;
                    string writeTopicPattern = writePropForm?.TopicPattern ?? string.Empty;

                    bool doesReadTargetMaintainer = DoesTopicReferToMaintainer(readTopicPattern);
                    bool doesWriteTargetMaintainer = DoesTopicReferToMaintainer(writeTopicPattern);

                    propertySpecs.Add(new PropertySpec(
                        schemaNamer,
                        propKvp.Key,
                        propSchema,
                        readFormat,
                        writeFormat,
                        readRespSchema,
                        writeReqSchema,
                        writeRespSchema,
                        propKvp.Key,
                        readErrorName,
                        readErrorSchema,
                        writeErrorName,
                        writeErrorSchema,
                        doesReadTargetMaintainer,
                        doesWriteTargetMaintainer,
                        isAggregate: false));

                    foreach (IEnvoyTemplateTransform transform in envoyFactory.GetPropertyTransforms(
                        schemaNamer,
                        serviceName,
                        propKvp.Key,
                        propSchema,
                        readRespSchema,
                        writeReqSchema,
                        writeRespSchema,
                        propKvp.Key,
                        readErrorName,
                        readErrorSchema,
                        writeErrorName,
                        writeErrorSchema,
                        readFormat,
                        writeFormat,
                        readPropForm?.TopicPattern ?? string.Empty,
                        writePropForm?.TopicPattern ?? string.Empty,
                        separateProperties: true,
                        doesReadTargetMaintainer,
                        doesWriteTargetMaintainer,
                        generateClient,
                        generateServer))
                    {
                        transforms[transform.FileName] = transform;
                    }
                }
            }

            string? readAllRespSchema = null;
            FormInfo? readAllPropsForm = FormInfo.CreateFromForm(errorReporter, tdThing.Forms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements?.Any(e => e.Value.Value == TDValues.OpReadAllProps) ?? false)?.Value, tdThing.SchemaDefinitions?.Entries);
            if (readAllPropsForm?.TopicPattern != null && readAllPropsForm.Format != SerializationFormat.None)
            {
                readAllRespSchema = readAllPropsForm.HasErrorResponse ? schemaNamer.AggregatePropReadRespSchema : schemaNamer.AggregatePropSchema;

                formattedTypesToSerialize[readAllPropsForm.Format].Add(readAllRespSchema);

                if (readAllPropsForm.HasErrorResponse)
                {
                    formattedTypesToSerialize[readAllPropsForm.Format].Add(schemaNamer.AggregatePropSchema);
                    formattedTypesToSerialize[readAllPropsForm.Format].Add(schemaNamer.AggregatePropReadErrSchema);
                    aggErrorSpecs[schemaNamer.AggregatePropReadErrSchema] = new AggregateErrorSpec(schemaNamer.AggregatePropReadErrSchema, readInnerErrors);
                }
            }

            string? writeMultiReqSchema = null;
            string? writeMultiRespSchema = null;
            FormInfo? writeMultPropsForm = FormInfo.CreateFromForm(errorReporter, tdThing.Forms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements?.Any(e => e.Value.Value == TDValues.OpWriteMultProps) ?? false)?.Value, tdThing.SchemaDefinitions?.Entries);
            if (writeMultPropsForm?.TopicPattern != null && writeMultPropsForm.Format != SerializationFormat.None)
            {
                writeMultiReqSchema = schemaNamer.AggregatePropWriteSchema;
                formattedTypesToSerialize[writeMultPropsForm.Format].Add(writeMultiReqSchema);

                if (writeMultPropsForm.HasErrorResponse)
                {
                    writeMultiRespSchema = schemaNamer.AggregatePropWriteRespSchema;
                    formattedTypesToSerialize[writeMultPropsForm.Format].Add(writeMultiRespSchema);
                    formattedTypesToSerialize[writeMultPropsForm.Format].Add(schemaNamer.AggregatePropWriteErrSchema);
                    aggErrorSpecs[schemaNamer.AggregatePropWriteErrSchema] = new AggregateErrorSpec(schemaNamer.AggregatePropWriteErrSchema, writeInnerErrors);
                }
            }

            if (readAllRespSchema != null || writeMultiReqSchema != null)
            {
                SerializationFormat readFormat = readAllPropsForm?.Format ?? SerializationFormat.None;
                SerializationFormat writeFormat = writeMultPropsForm?.Format ?? SerializationFormat.None;

                string? readErrorName = readAllPropsForm?.HasErrorResponse ?? false ? schemaNamer.AggregateRespErrorField : null;
                string? readErrorSchema = readAllPropsForm?.HasErrorResponse ?? false ? schemaNamer.AggregatePropReadErrSchema : null;
                string? writeErrorName = writeMultPropsForm?.HasErrorResponse ?? false ? schemaNamer.AggregateRespErrorField : null;
                string? writeErrorSchema = writeMultPropsForm?.HasErrorResponse ?? false ? schemaNamer.AggregatePropWriteErrSchema : null;

                string readTopicPattern = readAllPropsForm?.TopicPattern ?? string.Empty;
                string writeTopicPattern = writeMultPropsForm?.TopicPattern ?? string.Empty;

                bool doesReadTargetMaintainer = DoesTopicReferToMaintainer(readTopicPattern);
                bool doesWriteTargetMaintainer = DoesTopicReferToMaintainer(writeTopicPattern);

                propertySpecs.Add(new PropertySpec(
                    schemaNamer,
                    schemaNamer.AggregatePropName,
                    schemaNamer.AggregatePropSchema,
                    readFormat,
                    writeFormat,
                    readAllRespSchema,
                    writeMultiReqSchema,
                    writeMultiRespSchema,
                    schemaNamer.AggregateReadRespValueField,
                    readErrorName,
                    readErrorSchema,
                    writeErrorName,
                    writeErrorSchema,
                    doesReadTargetMaintainer,
                    doesWriteTargetMaintainer,
                    isAggregate: true));

                foreach (IEnvoyTemplateTransform transform in envoyFactory.GetPropertyTransforms(
                    schemaNamer,
                    serviceName,
                    schemaNamer.AggregatePropName,
                    schemaNamer.AggregatePropSchema,
                    readAllRespSchema,
                    writeMultiReqSchema,
                    writeMultiRespSchema,
                    schemaNamer.AggregateReadRespValueField,
                    readErrorName,
                    readErrorSchema,
                    writeErrorName,
                    writeErrorSchema,
                    readAllPropsForm?.Format ?? SerializationFormat.None,
                    writeMultPropsForm?.Format ?? SerializationFormat.None,
                    readAllPropsForm?.TopicPattern ?? string.Empty,
                    writeMultPropsForm?.TopicPattern ?? string.Empty,
                    separateProperties: false,
                    doesReadTargetMaintainer,
                    doesWriteTargetMaintainer,
                    generateClient,
                    generateServer))
                {
                    transforms[transform.FileName] = transform;
                }
            }

            return propertySpecs;
        }

        private static string? GetAndRecordError(string propName, FormInfo? form, SchemaNamer schemaNamer, Dictionary<string, ErrorSpec> errorSpecs, Dictionary<SerializationFormat, HashSet<string>> formattedTypesToSerialize, Dictionary<string, string> innerErrors)
        {
            if (form?.ErrorRespSchema == null)
            {
                return null;
            }

            string errSchemaName = schemaNamer.ChooseTitleOrName(form.ErrorRespSchema.Value.Title?.Value.Value, form.ErrorRespName)!;
            if (form.ErrorRespFormat != SerializationFormat.None)
            {
                formattedTypesToSerialize[form.ErrorRespFormat].Add(errSchemaName);
            }

            errorSpecs[errSchemaName] = new ErrorSpec(
                errSchemaName,
                form.ErrorRespSchema.Value.Description?.Value.Value ?? "The action could not be completed",
                form.ErrorRespSchema.Value.ErrorMessage?.Value.Value,
                form.ErrorRespSchema.Value.Required?.Elements?.Any(e => e.Value.Value == (form.ErrorRespSchema.Value.ErrorMessage?.Value.Value ?? string.Empty)) ?? false);
            innerErrors[propName] = errSchemaName;
            return errSchemaName;
        }

        private static bool DoesTopicReferToMaintainer(string? topic)
        {
            return topic != null && topic.Contains($"{{{MqttTopicTokens.PropertyMaintainerId}}}");
        }
    }
}
