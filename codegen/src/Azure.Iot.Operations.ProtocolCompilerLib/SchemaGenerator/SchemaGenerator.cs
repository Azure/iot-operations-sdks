namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using DTDLParser;
    using DTDLParser.Models;

    public class SchemaGenerator
    {
        private IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict;
        private string projectName;
        private DTInterfaceInfo dtInterface;
        private string payloadFormat;
        private CodeName genNamespace;
        private int mqttVersion;
        private string? telemetryTopic;
        private string? commandTopic;
        private string? telemServiceGroupId;
        private string? cmdServiceGroupId;
        private bool separateTelemetries;

        public SchemaGenerator(IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict, string projectName, DTInterfaceInfo dtInterface, int mqttVersion, CodeName genNamespace)
        {
            this.modelDict = modelDict;
            this.projectName = projectName;
            this.dtInterface = dtInterface;
            this.genNamespace = genNamespace;
            this.mqttVersion = mqttVersion;

            payloadFormat = (string)dtInterface.SupplementalProperties[string.Format(DtdlMqttExtensionValues.PayloadFormatPropertyFormat, mqttVersion)];

            telemetryTopic = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.TelemTopicPropertyFormat, mqttVersion), out object? telemTopicObj) ? (string)telemTopicObj : null;
            commandTopic = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.CmdReqTopicPropertyFormat, mqttVersion), out object? cmdTopicObj) ? (string)cmdTopicObj : null;
            separateTelemetries = telemetryTopic?.Contains(MqttTopicTokens.TelemetryName) ?? false;

            if (mqttVersion == 1)
            {
                telemServiceGroupId = null;
                cmdServiceGroupId = commandTopic != null && !commandTopic.Contains(MqttTopicTokens.CommandExecutorId) ? "MyServiceGroup" : null;
            }
            else
            {
                telemServiceGroupId = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.TelemServiceGroupIdPropertyFormat, mqttVersion), out object? telemServiceGroupIdObj) ? (string)telemServiceGroupIdObj : null;
                cmdServiceGroupId = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.CmdServiceGroupIdPropertyFormat, mqttVersion), out object? cmdServiceGroupIdObj) ? (string)cmdServiceGroupIdObj : null;
                if (commandTopic != null && commandTopic.Contains(MqttTopicTokens.CommandExecutorId) && cmdServiceGroupId != null)
                {
                    throw new Exception($"Model must not specify 'cmdServiceGroupId' property when 'commandTopic' includes token '{MqttTopicTokens.CommandExecutorId}'");
                }
            }
        }

        public string SerializationFormat { get => this.payloadFormat; }

        public void GenerateInterfaceAnnex(Action<string, string, string> acceptor, CodeName? sharedPrefix)
        {
            CodeName serviceName = new(dtInterface.Id);

            List<TelemetrySchemaInfo> telemSchemaInfos =
                !dtInterface.Telemetries.Any() ? new() :
                separateTelemetries ? dtInterface.Telemetries.Select(t => new TelemetrySchemaInfo((string?)t.Key, GetTelemSchema(t.Value))).ToList() :
                new() { new TelemetrySchemaInfo(null, GetAggregateTelemSchema()) };

            List<CommandSchemaInfo> cmdSchemaInfos = dtInterface.Commands.Values.Select(c => GetCommandSchemaInfo(c)).ToList();

            List<ErrorSchemaInfo> errSchemaInfos = modelDict.Values.Where(e => e.EntityKind == DTEntityKind.Object && IsObjectError((DTObjectInfo)e)).Select(e => (DTObjectInfo)e).Select(o => new ErrorSchemaInfo(new CodeName(o.Id), o.Description.FirstOrDefault().Value, GetErrorMessageSchema(o))).ToList();

            ITemplateTransform interfaceAnnexTransform = new InterfaceAnnex(projectName, genNamespace, sharedPrefix, dtInterface.Id.ToString(), payloadFormat, serviceName, telemetryTopic, commandTopic, telemServiceGroupId, cmdServiceGroupId, telemSchemaInfos, cmdSchemaInfos, errSchemaInfos, separateTelemetries);
            acceptor(interfaceAnnexTransform.TransformText(), interfaceAnnexTransform.FileName, interfaceAnnexTransform.FolderPath);
        }

        public void GenerateTelemetrySchemas(Action<string, string, string> acceptor, CodeName? sharedPrefix)
        {
            if (dtInterface.Telemetries.Any())
            {
                if (separateTelemetries)
                {
                    foreach (KeyValuePair<string, DTTelemetryInfo> dtTelemetry in dtInterface.Telemetries)
                    {
                        var nameDescSchemaRequiredIndices = new List<(string, string, DTSchemaInfo, bool, int)> { (dtTelemetry.Key, dtTelemetry.Value.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value ?? $"The '{dtTelemetry.Key}' Telemetry.", dtTelemetry.Value.Schema, true, 1) };
                        WriteTelemetrySchema(GetTelemSchema(dtTelemetry.Value), nameDescSchemaRequiredIndices, acceptor, sharedPrefix, isSeparate: true);
                    }
                }
                else
                {
                    List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices = dtInterface.Telemetries.Values.Select(t => (t.Name, t.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value ?? $"The '{t.Name}' Telemetry.", t.Schema, false, GetFieldIndex(t))).ToList();
                    nameDescSchemaRequiredIndices.Sort((x, y) => x.Item5 == 0 && y.Item5 == 0 ? x.Item1.CompareTo(y.Item1) : y.Item5.CompareTo(x.Item5));
                    int ix = nameDescSchemaRequiredIndices.FirstOrDefault().Item5;
                    nameDescSchemaRequiredIndices = nameDescSchemaRequiredIndices.Select(x => (x.Item1, x.Item2, x.Item3, x.Item4, x.Item5 == 0 ? ++ix : x.Item5)).ToList();
                    WriteTelemetrySchema(GetAggregateTelemSchema(), nameDescSchemaRequiredIndices, acceptor, sharedPrefix, isSeparate: false);
                }
            }
        }

        public void GenerateCommandSchemas(Action<string, string, string> acceptor, CodeName? sharedPrefix)
        {
            foreach (KeyValuePair<string, DTCommandInfo> dtCommand in dtInterface.Commands)
            {
                if (dtCommand.Value.Request != null && !IsCommandPayloadTransparent(dtCommand.Value.Request))
                {
                    ITypeName reqSchema = GetRequestSchema(dtCommand.Value)!;

                    foreach (ITemplateTransform reqSchemaTransform in SchemaTransformFactory.GetCommandSchemaTransforms(
                        payloadFormat, projectName, genNamespace, dtInterface.Id, reqSchema, dtCommand.Key, "request", dtCommand.Value.Request.Name, dtCommand.Value.Request.Schema, sharedPrefix, mqttVersion, dtCommand.Value.Request.Nullable))
                    {
                        acceptor(reqSchemaTransform.TransformText(), reqSchemaTransform.FileName, reqSchemaTransform.FolderPath);
                    }
                }

                if (dtCommand.Value.Response != null && !IsCommandPayloadTransparent(dtCommand.Value.Response))
                {
                    ITypeName respSchema = GetResponseSchema(dtCommand.Value)!;

                    string paramName = dtCommand.Value.Response.Name;
                    DTSchemaInfo? paramSchema = dtCommand.Value.Response.Schema;
                    if (IsSchemaResult(paramSchema))
                    {
                        DTFieldInfo? normalField = ((DTObjectInfo)paramSchema).Fields.FirstOrDefault(f => IsFieldNormalResult(f));
                        paramName = normalField?.Name ?? string.Empty;
                        paramSchema = normalField?.Schema;
                    }

                    if (paramSchema != null)
                    {
                        foreach (ITemplateTransform respSchemaTransform in SchemaTransformFactory.GetCommandSchemaTransforms(
                            payloadFormat, projectName, genNamespace, dtInterface.Id, respSchema, dtCommand.Key, "response", paramName, paramSchema, sharedPrefix, mqttVersion, dtCommand.Value.Response.Nullable))
                        {
                            acceptor(respSchemaTransform.TransformText(), respSchemaTransform.FileName, respSchemaTransform.FolderPath);
                        }
                    }
                }
            }
        }

        public void GenerateObjects(Action<string, string, string> acceptor, CodeName? sharedPrefix)
        {
            foreach (DTObjectInfo dtObject in modelDict.Values.Where(e => e.EntityKind == DTEntityKind.Object).Select(e => (DTObjectInfo)e))
            {
                GenerateObject(dtObject, acceptor, sharedPrefix, payloadFormat, projectName, genNamespace, dtInterface.Id);
            }
        }

        public void GenerateEnums(Action<string, string, string> acceptor, CodeName? sharedPrefix)
        {
            foreach (DTEnumInfo dtEnum in modelDict.Values.Where(e => e.EntityKind == DTEntityKind.Enum).Select(e => (DTEnumInfo)e))
            {
                List<(string, string, int)> nameValueIndices = dtEnum.EnumValues.Select(e => (e.Name, e.EnumValue.ToString()!, GetFieldIndex(e))).ToList();
                GenerateEnum(dtEnum, JsonSchemaSupport.GetPrimitiveType(dtEnum.ValueSchema.Id), nameValueIndices, acceptor, sharedPrefix, payloadFormat, projectName, genNamespace);
            }
        }

        public void GenerateArrays(Action<string, string, string> acceptor)
        {
            foreach (DTArrayInfo dtArray in modelDict.Values.Where(e => e.EntityKind == DTEntityKind.Array).Select(e => (DTArrayInfo)e))
            {
                CodeName schemaName = new(dtArray.Id);
                string? description = dtArray.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value;

                foreach (ITemplateTransform arraySchemaTransform in SchemaTransformFactory.GetArraySchemaTransforms(payloadFormat, projectName, genNamespace, dtInterface.Id, dtArray.ElementSchema, description, schemaName))
                {
                    acceptor(arraySchemaTransform.TransformText(), arraySchemaTransform.FileName, arraySchemaTransform.FolderPath);
                }
            }
        }

        public void GenerateMaps(Action<string, string, string> acceptor)
        {
            foreach (DTMapInfo dtMap in modelDict.Values.Where(e => e.EntityKind == DTEntityKind.Map).Select(e => (DTMapInfo)e))
            {
                CodeName schemaName = new(dtMap.Id);
                string? description = dtMap.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value;

                foreach (ITemplateTransform mapSchemaTransform in SchemaTransformFactory.GetMapSchemaTransforms(payloadFormat, projectName, genNamespace, dtInterface.Id, dtMap.MapValue.Schema, description, schemaName))
                {
                    acceptor(mapSchemaTransform.TransformText(), mapSchemaTransform.FileName, mapSchemaTransform.FolderPath);
                }
            }
        }

        public void GenerateJsonErrorFields(Action<string, string, string> acceptor, CodeName? sharedPrefix)
        {
            if (payloadFormat == PayloadFormat.Json)
            {
                return;
            }

            HashSet<Dtmi> generatedErrorCodeIds = new ();
            HashSet<Dtmi> generatedErrorInfoIds = new ();

            foreach (DTObjectInfo dtObject in modelDict.Values.Where(e => e.EntityKind == DTEntityKind.Object).Select(e => (DTObjectInfo)e))
            {
                foreach (DTFieldInfo dtField in dtObject.Fields)
                {
                    if (IsFieldErrorCode(dtField) && generatedErrorCodeIds.Add(dtField.Schema.Id))
                    {
                        List<(string, string, int)> nameValueIndices = ((DTEnumInfo)dtField.Schema).EnumValues.Select((e, i) => (e.Name, i.ToString(), GetFieldIndex(e))).ToList();
                        GenerateEnum((DTEnumInfo)dtField.Schema, "integer", nameValueIndices, acceptor, sharedPrefix, PayloadFormat.Json, projectName, genNamespace);
                    }
                    else if (IsFieldErrorInfo(dtField) && generatedErrorInfoIds.Add(dtField.Schema.Id))
                    {
                        GenerateObject((DTObjectInfo)dtField.Schema, acceptor, sharedPrefix, PayloadFormat.Json, projectName, genNamespace, dtInterface.Id);
                    }
                }
            }
        }

        public void CopyIncludedSchemas(Action<string, string, string> acceptor)
        {
            foreach (ITemplateTransform schemaTransform in SchemaTransformFactory.GetSchemaTransforms(payloadFormat))
            {
                acceptor(schemaTransform.TransformText(), schemaTransform.FileName, schemaTransform.FolderPath);
            }

            if (payloadFormat != PayloadFormat.Json && modelDict.Values.Any(e => e.EntityKind == DTEntityKind.Field && (IsFieldErrorCode((DTFieldInfo)e) || IsFieldErrorInfo((DTFieldInfo)e))))
            {
                foreach (ITemplateTransform schemaTransform in SchemaTransformFactory.GetSchemaTransforms(PayloadFormat.Json))
                {
                    acceptor(schemaTransform.TransformText(), schemaTransform.FileName, schemaTransform.FolderPath);
                }
            }
        }

        private void WriteTelemetrySchema(ITypeName telemSchema, List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices, Action<string, string, string> acceptor, CodeName? sharedPrefix, bool isSeparate)
        {
            foreach (ITemplateTransform templateTransform in SchemaTransformFactory.GetTelemetrySchemaTransforms(payloadFormat, projectName, genNamespace, dtInterface.Id, telemSchema, nameDescSchemaRequiredIndices, sharedPrefix, isSeparate, mqttVersion))
            {
                acceptor(templateTransform.TransformText(), templateTransform.FileName, templateTransform.FolderPath);
            }
        }

        private (CodeName?, bool, CodeName?, CodeName?, CodeName?, CodeName?) GetErrorMessageSchema(DTObjectInfo dtObject)
        {
            CodeName? messageFieldName = null;
            bool messageIsNullable = false;
            CodeName? errorCodeName = null;
            CodeName? errorCodeSchema = null;
            CodeName? errorInfoName = null;
            CodeName? errorInfoSchema = null;

            Dtmi errorMessageAdjunctTypeId = new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorMessageAdjunctTypeFormat, mqttVersion));
            DTFieldInfo? messageField = dtObject.Fields.FirstOrDefault(f => f.SupplementalTypes.Contains(errorMessageAdjunctTypeId));
            if (messageField != null)
            {
                messageFieldName = new CodeName(messageField.Id);
                messageIsNullable = !IsRequired(messageField);
            }

            DTFieldInfo? errorCodeField = dtObject.Fields.FirstOrDefault(f => IsFieldErrorCode(f));
            DTFieldInfo? errorInfoField = dtObject.Fields.FirstOrDefault(f => IsFieldErrorInfo(f));

            if (errorCodeField != null)
            {
                errorCodeName = new CodeName(errorCodeField.Name);
                errorCodeSchema = new CodeName(errorCodeField.Schema.Id);
            }

            if (errorInfoField != null)
            {
                errorInfoName = new CodeName(errorInfoField.Name);
                errorInfoSchema = new CodeName(errorInfoField.Schema.Id);
            }

            return (messageFieldName, messageIsNullable, errorCodeName, errorCodeSchema, errorInfoName, errorInfoSchema);
        }

        private ITypeName GetTelemSchema(DTTelemetryInfo dtTelem)
        {
            return payloadFormat switch
            {
                PayloadFormat.Raw => RawTypeName.Instance,
                PayloadFormat.Custom => CustomTypeName.Instance,
                _ => SchemaNames.GetTelemSchema(dtTelem.Name),
            };
        }

        private ITypeName GetAggregateTelemSchema()
        {
            return payloadFormat switch
            {
                PayloadFormat.Raw => RawTypeName.Instance,
                PayloadFormat.Custom => CustomTypeName.Instance,
                _ => SchemaNames.AggregateTelemSchema,
            };
        }

        private CommandSchemaInfo GetCommandSchemaInfo(DTCommandInfo dtCommand)
        {
            ITypeName? responseSchema = null;
            CodeName? normalResultName = null;
            CodeName? normalResultSchema = null;
            CodeName? errorResultName = null;
            CodeName? errorResultSchema = null;
            CodeName? errorCodeName = null;
            CodeName? errorCodeSchema = null;
            Dictionary<string, string> errorCodeEnumeration = new ();
            CodeName? errorInfoName = null;
            CodeName? errorInfoSchema = null;

            if (IsSchemaResult(dtCommand.Response?.Schema))
            {
                DTFieldInfo? normalField = ((DTObjectInfo)dtCommand.Response!.Schema).Fields.FirstOrDefault(f => IsFieldNormalResult(f));
                DTFieldInfo? errorField = ((DTObjectInfo)dtCommand.Response!.Schema).Fields.FirstOrDefault(f => IsFieldErrorResult(f));

                DTObjectInfo? errorObject = errorField != null ? (DTObjectInfo)errorField.Schema : (DTObjectInfo)dtCommand.Response!.Schema;

                DTFieldInfo? errorCodeField = errorObject.Fields.FirstOrDefault(f => IsFieldErrorCode(f));
                DTFieldInfo? errorInfoField = errorObject.Fields.FirstOrDefault(f => IsFieldErrorInfo(f));

                if (normalField == null)
                {
                    throw new Exception($"Object co-typed {DtdlMqttExtensionValues.GetStandardTerm(DtdlMqttExtensionValues.ResultAdjunctTypeFormat)} requires a Field co-typed {DtdlMqttExtensionValues.GetStandardTerm(DtdlMqttExtensionValues.NormalResultAdjunctTypeFormat)}");
                }

                if (errorField != null)
                {
                    responseSchema = new CodeName(dtCommand.Response.Schema.Id);
                    normalResultName = new CodeName(normalField.Name);
                    normalResultSchema = SchemaNames.GetCmdRespSchema(dtCommand.Name);
                    errorResultName = new CodeName(errorField.Name);
                    errorResultSchema = new CodeName(errorField.Schema.Id);
                }
                else
                {
                    responseSchema = GetResponseSchema(dtCommand);
                }

                if (errorCodeField != null)
                {
                    errorCodeName = new CodeName(errorCodeField.Name);
                    errorCodeSchema = new CodeName(errorCodeField.Schema.Id);
                    errorCodeEnumeration = ((DTEnumInfo)errorCodeField.Schema).EnumValues.ToDictionary(v => v.Name, v => v.EnumValue.ToString()!);
                }

                if (errorInfoField != null)
                {
                    errorInfoName = new CodeName(errorInfoField.Name);
                    errorInfoSchema = new CodeName(errorInfoField.Schema.Id);
                }
            }
            else
            {
                responseSchema = GetResponseSchema(dtCommand);
            }

            return new CommandSchemaInfo(
                dtCommand.Name,
                GetRequestSchema(dtCommand),
                responseSchema,
                normalResultName,
                normalResultSchema,
                errorResultName,
                errorResultSchema,
                errorCodeName,
                errorCodeSchema,
                errorCodeEnumeration,
                errorInfoName,
                errorInfoSchema,
                dtCommand.Request?.Nullable ?? false,
                dtCommand.Response?.Nullable ?? false,
                IsCommandIdempotent(dtCommand),
                GetTtl(dtCommand));
        }

        private ITypeName? GetRequestSchema(DTCommandInfo dtCommand)
        {
            return dtCommand.Request == null ? null : payloadFormat switch
            {
                PayloadFormat.Raw => RawTypeName.Instance,
                PayloadFormat.Custom => CustomTypeName.Instance,
                _ when IsCommandPayloadTransparent(dtCommand.Request) => new CodeName(dtCommand.Request.Schema.Id),
                _ => SchemaNames.GetCmdReqSchema(dtCommand.Name),
            };
        }

        private ITypeName? GetResponseSchema(DTCommandInfo dtCommand)
        {
            return dtCommand.Response == null ? null : payloadFormat switch
            {
                PayloadFormat.Raw => RawTypeName.Instance,
                PayloadFormat.Custom => CustomTypeName.Instance,
                _ when IsCommandPayloadTransparent(dtCommand.Response) => new CodeName(dtCommand.Response.Schema.Id),
                _ => SchemaNames.GetCmdRespSchema(dtCommand.Name),
            };
        }

        private bool IsCommandPayloadTransparent(DTCommandPayloadInfo dtCommandPayload)
        {
            return payloadFormat == PayloadFormat.Json
                && dtCommandPayload.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.TransparentAdjunctTypeFormat, mqttVersion)))
                && !IsSchemaResult(dtCommandPayload.Schema);
        }

        private void GenerateObject(DTObjectInfo dtObject, Action<string, string, string> acceptor, CodeName? sharedPrefix, string payloadFormat, string projectName, CodeName genNamespace, Dtmi interfaceId)
        {
            CodeName schemaName = new(dtObject.Id);
            string? description = dtObject.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value;
            bool isResult = IsSchemaResult(dtObject);

            if (isResult && !dtObject.Fields.Any(f => IsFieldErrorResult(f)))
            {
                return;
            }

            List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices = dtObject.Fields.Where(f => !IsFieldErrorCode(f) && !IsFieldErrorInfo(f)).Select(f => (f.Name, f.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value ?? $"The '{f.Name}' Field.", f.Schema, IsRequired(f), GetFieldIndex(f))).ToList();
            nameDescSchemaRequiredIndices.Sort((x, y) => x.Item5 == 0 && y.Item5 == 0 ? x.Item1.CompareTo(y.Item1) : y.Item5.CompareTo(x.Item5));
            int ix = nameDescSchemaRequiredIndices.FirstOrDefault().Item5;
            nameDescSchemaRequiredIndices = nameDescSchemaRequiredIndices.Select(x => (x.Item1, x.Item2, x.Item3, x.Item4, x.Item5 == 0 ? ++ix : x.Item5)).ToList();

            foreach (ITemplateTransform objectSchemaTransform in SchemaTransformFactory.GetObjectSchemaTransforms(payloadFormat, projectName, genNamespace, interfaceId, dtObject.Id, description, schemaName, nameDescSchemaRequiredIndices, sharedPrefix, mqttVersion, isResult))
            {
                acceptor(objectSchemaTransform.TransformText(), objectSchemaTransform.FileName, objectSchemaTransform.FolderPath);
            }
        }

        private void GenerateEnum(DTEnumInfo dtEnum, string valueSchema, List<(string, string, int)> nameValueIndices, Action<string, string, string> acceptor, CodeName? sharedPrefix, string payloadFormat, string projectName, CodeName genNamespace)
        {
            CodeName schemaName = new(dtEnum.Id);
            string? description = dtEnum.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value;

            nameValueIndices.Sort((x, y) => x.Item3 == 0 && y.Item3 == 0 ? x.Item1.CompareTo(y.Item1) : y.Item3.CompareTo(x.Item3));
            int ix = nameValueIndices.FirstOrDefault().Item3;
            nameValueIndices = nameValueIndices.Select(x => (x.Item1, x.Item2, x.Item3 == 0 ? ++ix : x.Item3)).ToList();

            foreach (ITemplateTransform enumSchemaTransform in SchemaTransformFactory.GetEnumSchemaTransforms(payloadFormat, projectName, genNamespace, dtEnum.Id, description, schemaName, valueSchema, nameValueIndices, sharedPrefix))
            {
                acceptor(enumSchemaTransform.TransformText(), enumSchemaTransform.FileName, enumSchemaTransform.FolderPath);
            }
        }

        private int GetFieldIndex(DTEntityInfo dtEntity)
        {
            return dtEntity.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.IndexedAdjunctTypeFormat, mqttVersion))) ? (int)dtEntity.SupplementalProperties[string.Format(DtdlMqttExtensionValues.IndexPropertyFormat, mqttVersion)] : 0;
        }

        private bool IsRequired(DTFieldInfo dtField)
        {
            return dtField.SupplementalTypes.Any(t => DtdlMqttExtensionValues.RequiredAdjunctTypeRegex.IsMatch(t.AbsoluteUri));
        }

        private bool IsCommandIdempotent(DTCommandInfo dtCommand)
        {
            return dtCommand.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.IdempotentAdjunctTypeFormat, mqttVersion)));
        }

        private bool IsObjectError(DTObjectInfo dtObject)
        {
            return dtObject.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorAdjunctTypeFormat, mqttVersion)));
        }

        private bool IsSchemaResult(DTSchemaInfo? dtSchema)
        {
            return dtSchema != null && dtSchema.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.ResultAdjunctTypeFormat, mqttVersion)));
        }

        private bool IsFieldNormalResult(DTFieldInfo dtField)
        {
            return dtField.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.NormalResultAdjunctTypeFormat, mqttVersion)));
        }

        private bool IsFieldErrorResult(DTFieldInfo dtField)
        {
            return dtField.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorResultAdjunctTypeFormat, mqttVersion)));
        }

        private bool IsFieldErrorCode(DTFieldInfo dtField)
        {
            return dtField.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorCodeAdjunctTypeFormat, mqttVersion)));
        }

        private bool IsFieldErrorInfo(DTFieldInfo dtField)
        {
            return dtField.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.ErrorInfoAdjunctTypeFormat, mqttVersion)));
        }

        private string? GetTtl(DTCommandInfo dtCommand)
        {
            return dtCommand.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.CacheableAdjunctTypeFormat, mqttVersion))) ? XmlConvert.ToString((TimeSpan)dtCommand.SupplementalProperties[string.Format(DtdlMqttExtensionValues.TtlPropertyFormat, mqttVersion)]) : null;
        }
    }
}
