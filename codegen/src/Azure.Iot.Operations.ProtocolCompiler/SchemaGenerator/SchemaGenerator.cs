namespace Azure.Iot.Operations.ProtocolCompiler
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
        private string genNamespace;
        private string? telemetryTopic;
        private string? commandTopic;
        private string? serviceGroupId;
        private bool separateTelemetries;

        public static void GenerateSchemas(IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict, Dtmi interfaceId, int mqttVersion, string projectName, DirectoryInfo workingDir, out string annexFile, out List<string> schemaFiles)
        {
            schemaFiles = new List<string>();

            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[interfaceId];

            TopicCollisionDetector telemetryTopicCollisionDetector = TopicCollisionDetector.GetTelemetryTopicCollisionDetector();
            TopicCollisionDetector commandTopicCollisionDetector = TopicCollisionDetector.GetCommandTopicCollisionDetector();

            telemetryTopicCollisionDetector.Check(dtInterface, dtInterface.Telemetries.Keys, mqttVersion);
            commandTopicCollisionDetector.Check(dtInterface, dtInterface.Commands.Keys, mqttVersion);

            var schemaGenerator = new SchemaGenerator(modelDict, projectName, dtInterface, mqttVersion);

            string genNamespace = NameFormatter.DtmiToNamespace(dtInterface.Id);

            List<string> annexFiles = new List<string>();
            schemaGenerator.GenerateInterfaceAnnex(GetWriter(workingDir.FullName, annexFiles), mqttVersion);
            annexFile = annexFiles.First();

            schemaFiles = new List<string>();
            schemaGenerator.GenerateTelemetrySchemas(GetWriter(workingDir.FullName, schemaFiles), mqttVersion);
            schemaGenerator.GenerateCommandSchemas(GetWriter(workingDir.FullName, schemaFiles), mqttVersion);
            schemaGenerator.GenerateObjects(GetWriter(workingDir.FullName, schemaFiles), mqttVersion);
            schemaGenerator.GenerateEnums(GetWriter(workingDir.FullName, schemaFiles), mqttVersion);
            schemaGenerator.GenerateArrays(GetWriter(workingDir.FullName, schemaFiles));
            schemaGenerator.GenerateMaps(GetWriter(workingDir.FullName, schemaFiles));
            schemaGenerator.CopyIncludedSchemas(GetWriter(workingDir.FullName));
        }

        public SchemaGenerator(IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict, string projectName, DTInterfaceInfo dtInterface, int mqttVersion)
        {
            this.modelDict = modelDict;
            this.projectName = projectName;
            this.dtInterface = dtInterface;

            payloadFormat = (string)dtInterface.SupplementalProperties[string.Format(DtdlMqttExtensionValues.PayloadFormatPropertyFormat, mqttVersion)];
            genNamespace = NameFormatter.DtmiToNamespace(dtInterface.Id);

            telemetryTopic = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.TelemTopicPropertyFormat, mqttVersion), out object? telemTopicObj) ? (string)telemTopicObj : null;
            commandTopic = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.CmdReqTopicPropertyFormat, mqttVersion), out object? cmdTopicObj) ? (string)cmdTopicObj : null;
            separateTelemetries = telemetryTopic?.Contains(MqttTopicTokens.TelemetryName) ?? false;

            if (mqttVersion == 1)
            {
                serviceGroupId = commandTopic != null && !commandTopic.Contains(MqttTopicTokens.CommandExecutorId) ? "MyServiceGroup" : null;
            }
            else
            {
                serviceGroupId = dtInterface.SupplementalProperties.TryGetValue(string.Format(DtdlMqttExtensionValues.ServiceGroupIdPropertyFormat, mqttVersion), out object? serviceGroupIdObj) ? (string)serviceGroupIdObj : null;
                if (commandTopic != null && commandTopic.Contains(MqttTopicTokens.CommandExecutorId) && serviceGroupId != null)
                {
                    throw new Exception($"Model must not specify 'serviceGroupId' property when 'commandTopic' includes token '{MqttTopicTokens.CommandExecutorId}'");
                }
            }
        }

        public void GenerateInterfaceAnnex(Action<string, string, string> acceptor, int mqttVersion)
        {
            string serviceName = NameFormatter.DtmiToServiceName(dtInterface.Id);

            List<(string?, string)> telemNameSchemas =
                !dtInterface.Telemetries.Any() ? new() :
                separateTelemetries ? dtInterface.Telemetries.Select(t => ((string?)t.Key, SchemaNames.GetTelemSchema(t.Key))).ToList() :
                new() { (null, SchemaNames.AggregateTelemSchema) };

            List<(string, string?, string?, bool, string?)> cmdNameReqRespIdemStales = dtInterface.Commands.Values.Select(c => (c.Name, GetRequestSchema(c, mqttVersion), GetResponseSchema(c, mqttVersion), IsCommandIdempotent(c, mqttVersion), GetTtl(c, mqttVersion))).ToList();

            ITemplateTransform interfaceAnnexTransform = new InterfaceAnnex(projectName, genNamespace, dtInterface.Id.ToString(), payloadFormat, serviceName, telemetryTopic, commandTopic, serviceGroupId, telemNameSchemas, cmdNameReqRespIdemStales);
            acceptor(interfaceAnnexTransform.TransformText(), interfaceAnnexTransform.FileName, interfaceAnnexTransform.FolderPath);
        }

        public void GenerateTelemetrySchemas(Action<string, string, string> acceptor, int mqttVersion)
        {
            if (dtInterface.Telemetries.Any())
            {
                if (separateTelemetries)
                {
                    foreach (KeyValuePair<string, DTTelemetryInfo> dtTelemetry in dtInterface.Telemetries)
                    {
                        var nameDescSchemaRequiredIndices = new List<(string, string, DTSchemaInfo, bool, int)> { (dtTelemetry.Key, dtTelemetry.Value.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value ?? $"The '{dtTelemetry.Key}' Telemetry.", dtTelemetry.Value.Schema, true, 1) };
                        WriteTelemetrySchema(SchemaNames.GetTelemSchema(dtTelemetry.Key), nameDescSchemaRequiredIndices, acceptor);
                    }
                }
                else
                {
                    List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices = dtInterface.Telemetries.Values.Select(t => (t.Name, t.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value ?? $"The '{t.Name}' Telemetry.", t.Schema, false, GetFieldIndex(t, mqttVersion))).ToList();
                    nameDescSchemaRequiredIndices.Sort((x, y) => x.Item5 == 0 && y.Item5 == 0 ? x.Item1.CompareTo(y.Item1) : y.Item5.CompareTo(x.Item5));
                    int ix = nameDescSchemaRequiredIndices.FirstOrDefault().Item5;
                    nameDescSchemaRequiredIndices = nameDescSchemaRequiredIndices.Select(x => (x.Item1, x.Item2, x.Item3, x.Item4, x.Item5 == 0 ? ++ix : x.Item5)).ToList();
                    WriteTelemetrySchema(SchemaNames.AggregateTelemSchema, nameDescSchemaRequiredIndices, acceptor);
                }
            }
        }

        public void GenerateCommandSchemas(Action<string, string, string> acceptor, int mqttVersion)
        {
            foreach (KeyValuePair<string, DTCommandInfo> dtCommand in dtInterface.Commands)
            {
                string? reqSchema = null;
                if (dtCommand.Value.Request != null && !IsCommandPayloadTransparent(dtCommand.Value.Request, mqttVersion))
                {
                    reqSchema = GetRequestSchema(dtCommand.Value, mqttVersion);

                    foreach (ITemplateTransform reqSchemaTransform in SchemaTransformFactory.GetCommandSchemaTransforms(
                        payloadFormat, projectName, genNamespace, dtInterface.Id, reqSchema!, dtCommand.Key, "request", dtCommand.Value.Request.Name, dtCommand.Value.Request.Schema, dtCommand.Value.Request.Nullable, NameFormatter.DtmiToNamespace(dtInterface.Id), NameFormatter.GetLanguageSafeString(dtInterface.Id.CompleteVersion.ToString())))
                    {
                        acceptor(reqSchemaTransform.TransformText(), reqSchemaTransform.FileName, reqSchemaTransform.FolderPath);
                    }
                }

                string? respSchema = null;
                if (dtCommand.Value.Response != null && !IsCommandPayloadTransparent(dtCommand.Value.Response, mqttVersion))
                {
                    respSchema = GetResponseSchema(dtCommand.Value, mqttVersion);

                    foreach (ITemplateTransform respSchemaTransform in SchemaTransformFactory.GetCommandSchemaTransforms(
                        payloadFormat, projectName, genNamespace, dtInterface.Id, respSchema!, dtCommand.Key, "response", dtCommand.Value.Response.Name, dtCommand.Value.Response.Schema, dtCommand.Value.Response.Nullable, NameFormatter.DtmiToNamespace(dtInterface.Id), NameFormatter.GetLanguageSafeString(dtInterface.Id.CompleteVersion.ToString())))
                    {
                        acceptor(respSchemaTransform.TransformText(), respSchemaTransform.FileName, respSchemaTransform.FolderPath);
                    }
                }
            }
        }

        public void GenerateObjects(Action<string, string, string> acceptor, int mqttVersion)
        {
            foreach (DTObjectInfo dtObject in modelDict.Values.Where(e => e.EntityKind == DTEntityKind.Object).Select(e => (DTObjectInfo)e))
            {
                string schemaName = NameFormatter.DtmiToSchemaName(dtObject.Id, dtInterface.Id, "Object");
                string? description = dtObject.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value;

                List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices = dtObject.Fields.Select(f => (f.Name, f.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value ?? $"The '{f.Name}' Field.", f.Schema, IsRequired(f), GetFieldIndex(f, mqttVersion))).ToList();
                nameDescSchemaRequiredIndices.Sort((x, y) => x.Item5 == 0 && y.Item5 == 0 ? x.Item1.CompareTo(y.Item1) : y.Item5.CompareTo(x.Item5));
                int ix = nameDescSchemaRequiredIndices.FirstOrDefault().Item5;
                nameDescSchemaRequiredIndices = nameDescSchemaRequiredIndices.Select(x => (x.Item1, x.Item2, x.Item3, x.Item4, x.Item5 == 0 ? ++ix : x.Item5)).ToList();

                foreach (ITemplateTransform objectSchemaTransform in SchemaTransformFactory.GetObjectSchemaTransforms(payloadFormat, projectName, genNamespace, dtInterface.Id, dtObject.Id, description, schemaName, nameDescSchemaRequiredIndices))
                {
                    acceptor(objectSchemaTransform.TransformText(), objectSchemaTransform.FileName, objectSchemaTransform.FolderPath);
                }
            }
        }

        public void GenerateEnums(Action<string, string, string> acceptor, int mqttVersion)
        {
            foreach (DTEnumInfo dtEnum in modelDict.Values.Where(e => e.EntityKind == DTEntityKind.Enum).Select(e => (DTEnumInfo)e))
            {
                string schemaName = NameFormatter.DtmiToSchemaName(dtEnum.Id, dtInterface.Id, "Enum");
                string? description = dtEnum.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value;

                List<(string, string, int)> nameValueIndices = dtEnum.EnumValues.Select(e => (e.Name, e.EnumValue.ToString()!, GetFieldIndex(e, mqttVersion))).ToList();
                nameValueIndices.Sort((x, y) => x.Item3 == 0 && y.Item3 == 0 ? x.Item1.CompareTo(y.Item1) : y.Item3.CompareTo(x.Item3));
                int ix = nameValueIndices.FirstOrDefault().Item3;
                nameValueIndices = nameValueIndices.Select(x => (x.Item1, x.Item2, x.Item3 == 0 ? ++ix : x.Item3)).ToList();

                foreach (ITemplateTransform enumSchemaTransform in SchemaTransformFactory.GetEnumSchemaTransforms(payloadFormat, projectName, genNamespace, dtEnum.Id, description, schemaName, dtEnum.ValueSchema.Id, nameValueIndices))
                {
                    acceptor(enumSchemaTransform.TransformText(), enumSchemaTransform.FileName, enumSchemaTransform.FolderPath);
                }
            }
        }

        public void GenerateArrays(Action<string, string, string> acceptor)
        {
            foreach (DTArrayInfo dtArray in modelDict.Values.Where(e => e.EntityKind == DTEntityKind.Array).Select(e => (DTArrayInfo)e))
            {
                string schemaName = NameFormatter.DtmiToSchemaName(dtArray.Id, dtInterface.Id, "Array");
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
                string schemaName = NameFormatter.DtmiToSchemaName(dtMap.Id, dtInterface.Id, "Map");
                string? description = dtMap.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value;

                foreach (ITemplateTransform mapSchemaTransform in SchemaTransformFactory.GetMapSchemaTransforms(payloadFormat, projectName, genNamespace, dtInterface.Id, dtMap.MapValue.Schema, description, schemaName))
                {
                    acceptor(mapSchemaTransform.TransformText(), mapSchemaTransform.FileName, mapSchemaTransform.FolderPath);
                }
            }
        }

        public void CopyIncludedSchemas(Action<string, string, string> acceptor)
        {
            foreach (ITemplateTransform schemaTransform in SchemaTransformFactory.GetSchemaTransforms(payloadFormat))
            {
                acceptor(schemaTransform.TransformText(), schemaTransform.FileName, schemaTransform.FolderPath);
            }
        }

        private void WriteTelemetrySchema(string telemSchema, List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices, Action<string, string, string> acceptor)
        {
            foreach (ITemplateTransform templateTransform in SchemaTransformFactory.GetTelemetrySchemaTransforms(payloadFormat, projectName, genNamespace, dtInterface.Id, telemSchema, nameDescSchemaRequiredIndices))
            {
                acceptor(templateTransform.TransformText(), templateTransform.FileName, templateTransform.FolderPath);
            }
        }

        private int GetFieldIndex(DTEntityInfo dtEntity, int mqttVersion)
        {
            return dtEntity.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.IndexedAdjunctTypeFormat, mqttVersion))) ? (int)dtEntity.SupplementalProperties[string.Format(DtdlMqttExtensionValues.IndexPropertyFormat, mqttVersion)] : 0;
        }

        private bool IsRequired(DTFieldInfo dtField)
        {
            return dtField.SupplementalTypes.Any(t => DtdlMqttExtensionValues.RequiredAdjunctTypeRegex.IsMatch(t.AbsoluteUri));
        }

        private string? GetRequestSchema(DTCommandInfo dtCommand, int mqttVersion)
        {
            return dtCommand.Request == null ? null :
                payloadFormat == PayloadFormat.Raw ? "" :
                IsCommandPayloadTransparent(dtCommand.Request, mqttVersion) ? NameFormatter.DtmiToSchemaName(dtCommand.Request.Schema.Id, dtInterface.Id, "Object") :
                SchemaNames.GetCmdReqSchema(dtCommand.Name);
        }

        private string? GetResponseSchema(DTCommandInfo dtCommand, int mqttVersion)
        {
            return dtCommand.Response == null ? null :
                payloadFormat == PayloadFormat.Raw ? "" :
                IsCommandPayloadTransparent(dtCommand.Response, mqttVersion) ? NameFormatter.DtmiToSchemaName(dtCommand.Response.Schema.Id, dtInterface.Id, "Object") :
                SchemaNames.GetCmdRespSchema(dtCommand.Name);
        }

        private static bool IsCommandIdempotent(DTCommandInfo dtCommand, int mqttVersion)
        {
            return dtCommand.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.IdempotentAdjunctTypeFormat, mqttVersion)));
        }

        private static string? GetTtl(DTCommandInfo dtCommand, int mqttVersion)
        {
            return dtCommand.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.CacheableAdjunctTypeFormat, mqttVersion))) ? XmlConvert.ToString((TimeSpan)dtCommand.SupplementalProperties[string.Format(DtdlMqttExtensionValues.TtlPropertyFormat, mqttVersion)]) : null;
        }

        private static bool IsCommandPayloadTransparent(DTCommandPayloadInfo dtCommandPayload, int mqttVersion)
        {
            return dtCommandPayload.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.TransparentAdjunctTypeFormat, mqttVersion)));
        }

        private static Action<string, string, string> GetWriter(string parentPath, List<string>? fileNames = null)
        {
            return (schemaText, fileName, subFolder) =>
            {
                string folderPath = Path.Combine(parentPath, subFolder);

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string filePath = Path.Combine(folderPath, fileName);
                File.WriteAllText(filePath, schemaText);
                Console.WriteLine($"  generated {filePath}");

                if (fileNames != null)
                {
                    fileNames.Add(fileName);
                }
            };
        }
    }
}
