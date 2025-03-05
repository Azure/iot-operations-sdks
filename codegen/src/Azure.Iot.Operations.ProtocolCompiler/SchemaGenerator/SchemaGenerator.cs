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
        private CodeName genNamespace;
        private string? telemetryTopic;
        private string? commandTopic;
        private string? telemServiceGroupId;
        private string? cmdServiceGroupId;
        private bool separateTelemetries;

        public static bool GenerateSchemas(IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict, Dtmi interfaceId, int mqttVersion, string projectName, DirectoryInfo workingDir, CodeName genNamespace, CodeName? sharedPrefix)
        {
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[interfaceId];

            TopicCollisionDetector telemetryTopicCollisionDetector = TopicCollisionDetector.GetTelemetryTopicCollisionDetector();
            TopicCollisionDetector commandTopicCollisionDetector = TopicCollisionDetector.GetCommandTopicCollisionDetector();

            telemetryTopicCollisionDetector.Check(dtInterface, dtInterface.Telemetries.Keys, mqttVersion);
            commandTopicCollisionDetector.Check(dtInterface, dtInterface.Commands.Keys, mqttVersion);

            var schemaGenerator = new SchemaGenerator(modelDict, projectName, dtInterface, mqttVersion, genNamespace);

            Dictionary<string, int> schemaCounts = new();

            var schemaWriter = new SchemaWriter(workingDir.FullName, schemaCounts);

            schemaGenerator.GenerateInterfaceAnnex(schemaWriter.Accept, mqttVersion, sharedPrefix);

            schemaGenerator.GenerateTelemetrySchemas(schemaWriter.Accept, mqttVersion, sharedPrefix);
            schemaGenerator.GenerateCommandSchemas(schemaWriter.Accept, mqttVersion, sharedPrefix);
            schemaGenerator.GenerateObjects(schemaWriter.Accept, mqttVersion, sharedPrefix);
            schemaGenerator.GenerateEnums(schemaWriter.Accept, mqttVersion, sharedPrefix);
            schemaGenerator.GenerateArrays(schemaWriter.Accept);
            schemaGenerator.GenerateMaps(schemaWriter.Accept);
            schemaGenerator.CopyIncludedSchemas(schemaWriter.Accept);

            if (schemaCounts.Any(kv => kv.Value > 1))
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Aborting schema generation due to duplicate generated names:");
                Console.ResetColor();
                foreach (KeyValuePair<string, int> schemaCount in schemaCounts.Where(kv => kv.Value > 1))
                {
                    Console.WriteLine($"  {schemaCount.Key}");
                }

                string exampleName = schemaCounts.FirstOrDefault(kv => kv.Value > 1 && kv.Key.EndsWith("Schema")).Key ?? "somethingSchema";
                string preName = exampleName.Substring(0, exampleName.Length - "Schema".Length);

                Console.WriteLine();
                Console.WriteLine(@"HINT: You can force a generated name by assigning an ""@id"" value, whose last label will determine the name, like this:");
                Console.WriteLine();
                Console.WriteLine($"    \"name\": \"{preName}\",");
                Console.WriteLine(@"    ""schema"": {");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(@"      ""@id"": ""dtmi:foo:bar:baz:SomeNameYouLike;1"",");
                Console.ResetColor();
                Console.WriteLine(@"      ""@type"": . . .");
                Console.WriteLine();

                Console.WriteLine(@"HINT: If your model contains a duplicated definition, you can outline it to the ""schemas"" section of the Interface, like this:");
                Console.WriteLine();
                Console.WriteLine(@"  ""schemas"": [");
                Console.WriteLine(@"    {");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"      \"@id\": \"dtmi:foo:bar:sharedSchemas:{exampleName};1\",");
                Console.ResetColor();
                Console.WriteLine(@"      ""@type"": . . .");
                Console.WriteLine(@"    }");
                Console.WriteLine(@"  ]");
                Console.WriteLine();
                Console.WriteLine(@"and then refer to the identifier (instead of an inline definition) from multiple places:");
                Console.WriteLine();
                Console.WriteLine($"    \"name\": \"{preName}\",");
                Console.Write(@"    ""schema"":");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($" \"dtmi:foo:bar:sharedSchemas:{exampleName};1\",");
                Console.ResetColor();

                Console.WriteLine();

                return false;
            }

            return true;
        }

        public SchemaGenerator(IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict, string projectName, DTInterfaceInfo dtInterface, int mqttVersion, CodeName genNamespace)
        {
            this.modelDict = modelDict;
            this.projectName = projectName;
            this.dtInterface = dtInterface;
            this.genNamespace = genNamespace;

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

        public void GenerateInterfaceAnnex(Action<string, string, string> acceptor, int mqttVersion, CodeName? sharedPrefix)
        {
            CodeName serviceName = new(dtInterface.Id);

            List<(string?, ITypeName)> telemNameSchemas =
                !dtInterface.Telemetries.Any() ? new() :
                separateTelemetries ? dtInterface.Telemetries.Select(t => ((string?)t.Key, GetTelemSchema(t.Value))).ToList() :
                new() { (null, GetAggregateTelemSchema()) };

            List<(string, ITypeName?, ITypeName?, bool, string?)> cmdNameReqRespIdemStales = dtInterface.Commands.Values.Select(c => (c.Name, GetRequestSchema(c, mqttVersion), GetResponseSchema(c, mqttVersion), IsCommandIdempotent(c, mqttVersion), GetTtl(c, mqttVersion))).ToList();

            ITemplateTransform interfaceAnnexTransform = new InterfaceAnnex(projectName, genNamespace, sharedPrefix, dtInterface.Id.ToString(), payloadFormat, serviceName, telemetryTopic, commandTopic, telemServiceGroupId, cmdServiceGroupId, telemNameSchemas, cmdNameReqRespIdemStales, separateTelemetries);
            acceptor(interfaceAnnexTransform.TransformText(), interfaceAnnexTransform.FileName, interfaceAnnexTransform.FolderPath);
        }

        public void GenerateTelemetrySchemas(Action<string, string, string> acceptor, int mqttVersion, CodeName? sharedPrefix)
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
                    List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices = dtInterface.Telemetries.Values.Select(t => (t.Name, t.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value ?? $"The '{t.Name}' Telemetry.", t.Schema, false, GetFieldIndex(t, mqttVersion))).ToList();
                    nameDescSchemaRequiredIndices.Sort((x, y) => x.Item5 == 0 && y.Item5 == 0 ? x.Item1.CompareTo(y.Item1) : y.Item5.CompareTo(x.Item5));
                    int ix = nameDescSchemaRequiredIndices.FirstOrDefault().Item5;
                    nameDescSchemaRequiredIndices = nameDescSchemaRequiredIndices.Select(x => (x.Item1, x.Item2, x.Item3, x.Item4, x.Item5 == 0 ? ++ix : x.Item5)).ToList();
                    WriteTelemetrySchema(GetAggregateTelemSchema(), nameDescSchemaRequiredIndices, acceptor, sharedPrefix, isSeparate: false);
                }
            }
        }

        public void GenerateCommandSchemas(Action<string, string, string> acceptor, int mqttVersion, CodeName? sharedPrefix)
        {
            foreach (KeyValuePair<string, DTCommandInfo> dtCommand in dtInterface.Commands)
            {
                if (dtCommand.Value.Request != null && !IsCommandPayloadTransparent(dtCommand.Value.Request, mqttVersion))
                {
                    ITypeName reqSchema = GetRequestSchema(dtCommand.Value, mqttVersion)!;

                    foreach (ITemplateTransform reqSchemaTransform in SchemaTransformFactory.GetCommandSchemaTransforms(
                        payloadFormat, projectName, genNamespace, dtInterface.Id, reqSchema, dtCommand.Key, "request", dtCommand.Value.Request.Name, dtCommand.Value.Request.Schema, sharedPrefix, dtCommand.Value.Request.Nullable))
                    {
                        acceptor(reqSchemaTransform.TransformText(), reqSchemaTransform.FileName, reqSchemaTransform.FolderPath);
                    }
                }

                if (dtCommand.Value.Response != null && !IsCommandPayloadTransparent(dtCommand.Value.Response, mqttVersion))
                {
                    ITypeName respSchema = GetResponseSchema(dtCommand.Value, mqttVersion)!;

                    foreach (ITemplateTransform respSchemaTransform in SchemaTransformFactory.GetCommandSchemaTransforms(
                        payloadFormat, projectName, genNamespace, dtInterface.Id, respSchema, dtCommand.Key, "response", dtCommand.Value.Response.Name, dtCommand.Value.Response.Schema, sharedPrefix, dtCommand.Value.Response.Nullable))
                    {
                        acceptor(respSchemaTransform.TransformText(), respSchemaTransform.FileName, respSchemaTransform.FolderPath);
                    }
                }
            }
        }

        public void GenerateObjects(Action<string, string, string> acceptor, int mqttVersion, CodeName? sharedPrefix)
        {
            foreach (DTObjectInfo dtObject in modelDict.Values.Where(e => e.EntityKind == DTEntityKind.Object).Select(e => (DTObjectInfo)e))
            {
                CodeName schemaName = new(dtObject.Id);
                string? description = dtObject.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value;

                List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices = dtObject.Fields.Select(f => (f.Name, f.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value ?? $"The '{f.Name}' Field.", f.Schema, IsRequired(f), GetFieldIndex(f, mqttVersion))).ToList();
                nameDescSchemaRequiredIndices.Sort((x, y) => x.Item5 == 0 && y.Item5 == 0 ? x.Item1.CompareTo(y.Item1) : y.Item5.CompareTo(x.Item5));
                int ix = nameDescSchemaRequiredIndices.FirstOrDefault().Item5;
                nameDescSchemaRequiredIndices = nameDescSchemaRequiredIndices.Select(x => (x.Item1, x.Item2, x.Item3, x.Item4, x.Item5 == 0 ? ++ix : x.Item5)).ToList();

                foreach (ITemplateTransform objectSchemaTransform in SchemaTransformFactory.GetObjectSchemaTransforms(payloadFormat, projectName, genNamespace, dtInterface.Id, dtObject.Id, description, schemaName, nameDescSchemaRequiredIndices, sharedPrefix))
                {
                    acceptor(objectSchemaTransform.TransformText(), objectSchemaTransform.FileName, objectSchemaTransform.FolderPath);
                }
            }
        }

        public void GenerateEnums(Action<string, string, string> acceptor, int mqttVersion, CodeName? sharedPrefix)
        {
            foreach (DTEnumInfo dtEnum in modelDict.Values.Where(e => e.EntityKind == DTEntityKind.Enum).Select(e => (DTEnumInfo)e))
            {
                CodeName schemaName = new(dtEnum.Id);
                string? description = dtEnum.Description.FirstOrDefault(t => t.Key.StartsWith("en")).Value;

                List<(string, string, int)> nameValueIndices = dtEnum.EnumValues.Select(e => (e.Name, e.EnumValue.ToString()!, GetFieldIndex(e, mqttVersion))).ToList();
                nameValueIndices.Sort((x, y) => x.Item3 == 0 && y.Item3 == 0 ? x.Item1.CompareTo(y.Item1) : y.Item3.CompareTo(x.Item3));
                int ix = nameValueIndices.FirstOrDefault().Item3;
                nameValueIndices = nameValueIndices.Select(x => (x.Item1, x.Item2, x.Item3 == 0 ? ++ix : x.Item3)).ToList();

                foreach (ITemplateTransform enumSchemaTransform in SchemaTransformFactory.GetEnumSchemaTransforms(payloadFormat, projectName, genNamespace, dtEnum.Id, description, schemaName, dtEnum.ValueSchema.Id, nameValueIndices, sharedPrefix))
                {
                    acceptor(enumSchemaTransform.TransformText(), enumSchemaTransform.FileName, enumSchemaTransform.FolderPath);
                }
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

        public void CopyIncludedSchemas(Action<string, string, string> acceptor)
        {
            foreach (ITemplateTransform schemaTransform in SchemaTransformFactory.GetSchemaTransforms(payloadFormat))
            {
                acceptor(schemaTransform.TransformText(), schemaTransform.FileName, schemaTransform.FolderPath);
            }
        }

        private void WriteTelemetrySchema(ITypeName telemSchema, List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices, Action<string, string, string> acceptor, CodeName? sharedPrefix, bool isSeparate)
        {
            foreach (ITemplateTransform templateTransform in SchemaTransformFactory.GetTelemetrySchemaTransforms(payloadFormat, projectName, genNamespace, dtInterface.Id, telemSchema, nameDescSchemaRequiredIndices, sharedPrefix, isSeparate))
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

        private ITypeName? GetRequestSchema(DTCommandInfo dtCommand, int mqttVersion)
        {
            return dtCommand.Request == null ? null : payloadFormat switch
            {
                PayloadFormat.Raw => RawTypeName.Instance,
                PayloadFormat.Custom => CustomTypeName.Instance,
                _ when IsCommandPayloadTransparent(dtCommand.Request, mqttVersion) => new CodeName(dtCommand.Request.Schema.Id),
                _ => SchemaNames.GetCmdReqSchema(dtCommand.Name),
            };
        }

        private ITypeName? GetResponseSchema(DTCommandInfo dtCommand, int mqttVersion)
        {
            return dtCommand.Response == null ? null : payloadFormat switch
            {
                PayloadFormat.Raw => RawTypeName.Instance,
                PayloadFormat.Custom => CustomTypeName.Instance,
                _ when IsCommandPayloadTransparent(dtCommand.Response, mqttVersion) => new CodeName(dtCommand.Response.Schema.Id),
                _ => SchemaNames.GetCmdRespSchema(dtCommand.Name),
            };
        }

        private bool IsCommandPayloadTransparent(DTCommandPayloadInfo dtCommandPayload, int mqttVersion)
        {
            return payloadFormat == PayloadFormat.Json && dtCommandPayload.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.TransparentAdjunctTypeFormat, mqttVersion)));
        }

        private static bool IsCommandIdempotent(DTCommandInfo dtCommand, int mqttVersion)
        {
            return dtCommand.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.IdempotentAdjunctTypeFormat, mqttVersion)));
        }

        private static string? GetTtl(DTCommandInfo dtCommand, int mqttVersion)
        {
            return dtCommand.SupplementalTypes.Contains(new Dtmi(string.Format(DtdlMqttExtensionValues.CacheableAdjunctTypeFormat, mqttVersion))) ? XmlConvert.ToString((TimeSpan)dtCommand.SupplementalProperties[string.Format(DtdlMqttExtensionValues.TtlPropertyFormat, mqttVersion)]) : null;
        }
    }
}
