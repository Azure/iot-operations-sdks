namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class PropertyEnvoyGenerator
    {
        internal static List<PropertySpec> GeneratePropertyEnvoys(TDThing tdThing, SchemaNamer schemaNamer, CodeName serviceName, EnvoyTransformFactory envoyFactory, Dictionary<string, IEnvoyTemplateTransform> transforms, Dictionary<string, ErrorSpec> errorSpecs, Dictionary<string, AggregateErrorSpec> aggErrorSpecs, HashSet<string> typesToSerialize)
        {
            List<PropertySpec> propertySpecs = new();
            Dictionary<string, string> readInnerErrors = new();
            Dictionary<string, string> writeInnerErrors = new();

            foreach (KeyValuePair<string, TDProperty> propKvp in tdThing.Properties ?? new())
            {
                FormInfo? readPropForm = FormInfo.CreateFromForm(propKvp.Value.Forms?.FirstOrDefault(f => f.Op?.Values.Contains(TDValues.OpReadProp) ?? false), tdThing.SchemaDefinitions);
                FormInfo? writePropForm = FormInfo.CreateFromForm(propKvp.Value.Forms?.FirstOrDefault(f => f.Op?.Values.Contains(TDValues.OpWriteProp) ?? false), tdThing.SchemaDefinitions);
                FormInfo? noOpForm = FormInfo.CreateFromForm(propKvp.Value.Forms?.FirstOrDefault(f => f.Op == null), tdThing.SchemaDefinitions);
                readPropForm ??= noOpForm;
                writePropForm ??= noOpForm;

                string propSchema = schemaNamer.GetPropSchema(propKvp.Key);
                string? readRespSchema = null;
                string? readErrorSchema = GetAndRecordError(propKvp.Key, readPropForm, schemaNamer, errorSpecs, typesToSerialize, readInnerErrors);
                if (readPropForm?.TopicPattern != null && readPropForm.Format != SerializationFormat.None)
                {
                    readRespSchema = readPropForm.ErrorRespSchema != null ? schemaNamer.GetPropReadRespSchema(propKvp.Key) : propSchema;
                    typesToSerialize.Add(readRespSchema);
                    typesToSerialize.Add(propSchema);
                }

                string? writeReqSchema = null;
                string? writeRespSchema = null;
                string? writeErrorSchema = null;
                if (!propKvp.Value.ReadOnly)
                {
                    if (writePropForm?.TopicPattern != null && writePropForm.Format != SerializationFormat.None)
                    {
                        writeReqSchema = propKvp.Value.Placeholder ? schemaNamer.GetWritablePropSchema(propKvp.Key) : propSchema;
                        typesToSerialize.Add(writeReqSchema);

                        if (writePropForm.HasErrorResponse)
                        {
                            writeRespSchema = schemaNamer.GetPropWriteRespSchema(propKvp.Key);
                            typesToSerialize.Add(writeRespSchema);
                        }
                    }

                    writeErrorSchema = GetAndRecordError(propKvp.Key, writePropForm, schemaNamer, errorSpecs, typesToSerialize, writeInnerErrors);
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
                        tdThing.Id!,
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
                        doesWriteTargetMaintainer))
                    {
                        transforms[transform.FileName] = transform;
                    }
                }
            }

            string? readAllRespSchema = null;
            FormInfo? readAllPropsForm = FormInfo.CreateFromForm(tdThing.Forms?.FirstOrDefault(f => f.Op?.Values.Contains(TDValues.OpReadAllProps) ?? false), tdThing.SchemaDefinitions);
            if (readAllPropsForm?.TopicPattern != null && readAllPropsForm.Format != SerializationFormat.None)
            {
                readAllRespSchema = readAllPropsForm.HasErrorResponse ? schemaNamer.AggregatePropReadRespSchema : schemaNamer.AggregatePropSchema;

                typesToSerialize.Add(readAllRespSchema);

                if (readAllPropsForm.HasErrorResponse)
                {
                    typesToSerialize.Add(schemaNamer.AggregatePropSchema);
                    typesToSerialize.Add(schemaNamer.AggregatePropReadErrSchema);
                    aggErrorSpecs[schemaNamer.AggregatePropReadErrSchema] = new AggregateErrorSpec(schemaNamer.AggregatePropReadErrSchema, readInnerErrors);
                }
            }

            string? writeMultiReqSchema = null;
            string? writeMultiRespSchema = null;
            FormInfo? writeMultPropsForm = FormInfo.CreateFromForm(tdThing.Forms?.FirstOrDefault(f => f.Op?.Values.Contains(TDValues.OpWriteMultProps) ?? false), tdThing.SchemaDefinitions);
            if (writeMultPropsForm?.TopicPattern != null && writeMultPropsForm.Format != SerializationFormat.None)
            {
                writeMultiReqSchema = schemaNamer.AggregatePropWriteSchema;
                typesToSerialize.Add(writeMultiReqSchema);

                if (writeMultPropsForm.HasErrorResponse)
                {
                    writeMultiRespSchema = schemaNamer.AggregatePropWriteRespSchema;
                    typesToSerialize.Add(writeMultiRespSchema);
                    typesToSerialize.Add(schemaNamer.AggregatePropWriteErrSchema);
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
                    tdThing.Id!,
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
                    doesWriteTargetMaintainer))
                {
                    transforms[transform.FileName] = transform;
                }
            }

            return propertySpecs;
        }

        private static string? GetAndRecordError(string propName, FormInfo? form, SchemaNamer schemaNamer, Dictionary<string, ErrorSpec> errorSpecs, HashSet<string> typesToSerialize, Dictionary<string, string> innerErrors)
        {
            if (form?.ErrorRespSchema == null)
            {
                return null;
            }

            string errSchemaName = schemaNamer.ChooseTitleOrName(form.ErrorRespSchema.Title, form.ErrorRespName)!;
            typesToSerialize.Add(errSchemaName);
            errorSpecs[errSchemaName] = new ErrorSpec(
                errSchemaName,
                form.ErrorRespSchema.Description ?? "The action could not be completed",
                form.ErrorRespSchema.ErrorMessage,
                form.ErrorRespSchema.Required?.Contains(form.ErrorRespSchema.ErrorMessage ?? string.Empty) ?? false);
            innerErrors[propName] = errSchemaName;
            return errSchemaName;
        }

        private static bool DoesTopicReferToMaintainer(string? topic)
        {
            return topic != null && topic.Contains(MqttTopicTokens.PropertyMaintainerId);
        }
    }
}
