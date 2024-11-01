namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    public static class EnvoyTransformFactory
    {
        private static readonly Dictionary<string, SerializerValues> formatSerializers = new()
        {
            { PayloadFormat.Avro, new SerializerValues("AVRO", "AvroSerializer{0}", "EmptyAvro") },
            { PayloadFormat.Cbor, new SerializerValues("CBOR", "CborSerializer", "EmptyCbor") },
            { PayloadFormat.Json, new SerializerValues("JSON", "Utf8JsonSerializer", "EmptyJson") },
            { PayloadFormat.Proto2, new SerializerValues("protobuf", "ProtobufSerializer{0}", "Google.Protobuf.WellKnownTypes.Empty") },
            { PayloadFormat.Proto3, new SerializerValues("protobuf", "ProtobufSerializer{0}", "Google.Protobuf.WellKnownTypes.Empty") },
            { PayloadFormat.Raw, new SerializerValues("raw", "PassthroughSerializer", "") },
        };

        public static IEnumerable<ITemplateTransform> GetTransforms(string language, string projectName, JsonDocument annexDocument, string? workingPath, string? sdkPath, bool syncApi, HashSet<string> sourceFilePaths)
        {
            string modelId = annexDocument.RootElement.GetProperty(AnnexFileProperties.ModelId).GetString()!;
            string genNamespace = annexDocument.RootElement.GetProperty(AnnexFileProperties.Namespace).GetString()!;
            string serviceName = annexDocument.RootElement.GetProperty(AnnexFileProperties.ServiceName).GetString()!;
            string genFormat = annexDocument.RootElement.GetProperty(AnnexFileProperties.PayloadFormat).GetString()!;

            string? telemetryTopic = annexDocument.RootElement.TryGetProperty(AnnexFileProperties.TelemetryTopic, out JsonElement telemTopicElt) ? telemTopicElt.GetString() : null;
            string? commandTopic = annexDocument.RootElement.TryGetProperty(AnnexFileProperties.CommandRequestTopic, out JsonElement cmdTopicElt) ? cmdTopicElt.GetString() : null;
            string? telemServiceGroupId = annexDocument.RootElement.TryGetProperty(AnnexFileProperties.TelemServiceGroupId, out JsonElement tGroupIdElt) ? tGroupIdElt.GetString() : null;
            string? cmdServiceGroupId = annexDocument.RootElement.TryGetProperty(AnnexFileProperties.CmdServiceGroupId, out JsonElement cGroupIdElt) ? cGroupIdElt.GetString() : null;

            string? version = modelId.IndexOf(";") > 0 ? modelId.Substring(modelId.IndexOf(";") + 1) : null;
            string? normalizedVersionSuffix = version?.Replace(".", "_");

            List<(string, string?, string?)> cmdNameReqResps = new();
            List<string> telemSchemas = new();

            List<string> schemaTypes = new();

            if (annexDocument.RootElement.TryGetProperty(AnnexFileProperties.TelemetryList, out JsonElement telemsElt) && telemsElt.GetArrayLength() > 0)
            {
                if (telemetryTopic == null)
                {
                    throw new Exception($"Model {modelId} has at least one Telemetry content but no {DtdlMqttExtensionValues.GetStandardTerm(DtdlMqttExtensionValues.TelemTopicPropertyFormat)} property");
                }

                foreach (JsonElement telemEl in telemsElt.EnumerateArray())
                {
                    foreach (ITemplateTransform templateTransform in GetTelemetryTransforms(language, projectName, genNamespace, modelId, serviceName, genFormat, telemEl, telemSchemas, workingPath, schemaTypes))
                    {
                        yield return templateTransform;
                    }
                }
            }

            if (annexDocument.RootElement.TryGetProperty(AnnexFileProperties.CommandList, out JsonElement cmdsElt) && cmdsElt.GetArrayLength() > 0)
            {
                if (commandTopic == null)
                {
                    throw new Exception($"Model {modelId} has at least one Command content but no {DtdlMqttExtensionValues.GetStandardTerm(DtdlMqttExtensionValues.CmdReqTopicPropertyFormat)} property");
                }

                foreach (JsonElement cmdEl in cmdsElt.EnumerateArray())
                {
                    foreach (ITemplateTransform templateTransform in GetCommandTransforms(language, projectName, genNamespace, modelId, serviceName, genFormat, commandTopic, cmdEl, cmdNameReqResps, normalizedVersionSuffix, workingPath, schemaTypes))
                    {
                        yield return templateTransform;
                    }
                }
            }

            foreach (ITemplateTransform templateTransform in GetServiceTransforms(language, projectName, genNamespace, modelId, serviceName, genFormat, commandTopic, telemetryTopic, cmdServiceGroupId, telemServiceGroupId, cmdNameReqResps, telemSchemas, sourceFilePaths, syncApi))
            {
                yield return templateTransform;
            }

            foreach (ITemplateTransform templateTransform in GetResourceTransforms(language, projectName, genFormat))
            {
                yield return templateTransform;
            }

            foreach (ITemplateTransform templateTransform in GetProjectTransforms(language, projectName, genNamespace, genFormat, sdkPath, sourceFilePaths, schemaTypes))
            {
                yield return templateTransform;
            }
        }

        private static IEnumerable<ITemplateTransform> GetTelemetryTransforms(string language, string projectName, string genNamespace, string modelId, string serviceName, string genFormat, JsonElement telemElt, List<string> telemSchemas, string? workingPath, List<string> schemaTypes)
        {
            string serializerSubNamespace = formatSerializers[genFormat].SubNamespace;
            string serializerClassName = formatSerializers[genFormat].ClassName;
            string serializerEmptyType = formatSerializers[genFormat].EmptyType;

            string? telemetryName = telemElt.TryGetProperty(AnnexFileProperties.TelemName, out JsonElement nameElt) ? nameElt.GetString() : null;
            string schemaClass = telemElt.GetProperty(AnnexFileProperties.TelemSchema).GetString()!;

            switch (language)
            {
                case "csharp":
                    yield return new DotNetTelemetrySender(telemetryName, projectName, genNamespace, modelId, serviceName, serializerSubNamespace, serializerClassName, serializerEmptyType, schemaClass);
                    yield return new DotNetTelemetryReceiver(telemetryName, projectName, genNamespace, modelId, serviceName, serializerSubNamespace, serializerClassName, serializerEmptyType, schemaClass);
                    break;
                case "go":
                    yield return new GoTelemetrySender(telemetryName, genNamespace, serializerSubNamespace, schemaClass);
                    yield return new GoTelemetryReceiver(telemetryName, genNamespace, serializerSubNamespace, schemaClass);
                    break;
                case "java":
                    yield return new JavaTelemetrySender(telemetryName, genNamespace, serializerSubNamespace, serializerClassName, schemaClass);
                    yield return new JavaTelemetryReceiver(telemetryName, genNamespace, serializerSubNamespace, serializerClassName, schemaClass);
                    break;
                case "python":
                    yield return new PythonTelemetrySender(telemetryName, genNamespace, serializerSubNamespace, serializerClassName, schemaClass);
                    yield return new PythonTelemetryReceiver(telemetryName, genNamespace, serializerSubNamespace, serializerClassName, schemaClass);
                    break;
                case "rust":
                    yield return new RustTelemetrySender(telemetryName, genNamespace, schemaClass);
                    yield return new RustTelemetryReceiver(telemetryName, genNamespace, schemaClass);
                    if (schemaClass != string.Empty)
                    {
                        schemaTypes.Add(schemaClass);
                        yield return new RustSerialization(genNamespace, genFormat, schemaClass, workingPath);
                    }
                    break;
                case "c":
                    break;
                default:
                    throw GetLanguageNotRecognizedException(language);
            }

            telemSchemas.Add(schemaClass);
        }

        private static IEnumerable<ITemplateTransform> GetCommandTransforms(string language, string projectName, string genNamespace, string modelId, string serviceName, string genFormat, string? commandTopic, JsonElement cmdElt, List<(string, string?, string?)> cmdNameReqResps, string? normalizedVersionSuffix, string? workingPath, List<string> schemaTypes)
        {
            bool doesCommandTargetExecutor = DoesTopicReferToExecutor(commandTopic);
            string serializerSubNamespace = formatSerializers[genFormat].SubNamespace;
            string serializerClassName = formatSerializers[genFormat].ClassName;
            string serializerEmptyType = formatSerializers[genFormat].EmptyType;

            string commandName = cmdElt.GetProperty(AnnexFileProperties.CommandName).GetString()!;
            string? reqSchemaClass = cmdElt.GetProperty(AnnexFileProperties.CmdRequestSchema).GetString();
            string? respSchemaClass = cmdElt.GetProperty(AnnexFileProperties.CmdResponseSchema).GetString();
            bool isIdempotent = cmdElt.GetProperty(AnnexFileProperties.CmdIsIdempotent).GetBoolean();
            string? cacheability = cmdElt.GetProperty(AnnexFileProperties.Cacheability).GetString();

            switch (language)
            {
                case "csharp":
                    yield return new DotNetCommandInvoker(commandName, projectName, genNamespace, modelId, serviceName, serializerSubNamespace, serializerClassName, serializerEmptyType, reqSchemaClass, respSchemaClass);
                    yield return new DotNetCommandExecutor(commandName, projectName, genNamespace, modelId, serviceName, serializerSubNamespace, serializerClassName, serializerEmptyType, reqSchemaClass, respSchemaClass, isIdempotent, cacheability);
                    break;
                case "go":
                    yield return new GoCommandInvoker(commandName, genNamespace, serializerSubNamespace, reqSchemaClass, respSchemaClass, doesCommandTargetExecutor);
                    yield return new GoCommandExecutor(commandName, genNamespace, serializerSubNamespace, reqSchemaClass, respSchemaClass, isIdempotent, cacheability);
                    break;
                case "java":
                    yield return new JavaCommandInvoker(commandName, genNamespace, serializerSubNamespace, serializerClassName, reqSchemaClass, respSchemaClass);
                    yield return new JavaCommandExecutor(commandName, genNamespace, serializerSubNamespace, serializerClassName, reqSchemaClass, respSchemaClass);
                    break;
                case "python":
                    yield return new PythonCommandInvoker(commandName, genNamespace, serializerSubNamespace, serializerClassName, reqSchemaClass, respSchemaClass);
                    yield return new PythonCommandExecutor(commandName, genNamespace, serializerSubNamespace, serializerClassName, reqSchemaClass, respSchemaClass);
                    break;
                case "rust":
                    yield return new RustCommandInvoker(commandName, genNamespace, serializerEmptyType, reqSchemaClass, respSchemaClass, doesCommandTargetExecutor);
                    yield return new RustCommandExecutor(commandName, genNamespace, serializerEmptyType, reqSchemaClass, respSchemaClass, isIdempotent, cacheability);
                    if (reqSchemaClass != null && reqSchemaClass != string.Empty)
                    {
                        schemaTypes.Add(reqSchemaClass);
                        yield return new RustSerialization(genNamespace, genFormat, reqSchemaClass, workingPath);
                    }
                    if (respSchemaClass != null && respSchemaClass != string.Empty)
                    {
                        schemaTypes.Add(respSchemaClass);
                        yield return new RustSerialization(genNamespace, genFormat, respSchemaClass, workingPath);
                    }
                    break;
                case "c":
                    yield return new CCommandInvoker(modelId, commandName, commandTopic!, genNamespace, serviceName, serializerSubNamespace, serializerClassName, reqSchemaClass, respSchemaClass, normalizedVersionSuffix);
                    yield return new CCommandExecutor(modelId, commandName, commandTopic!, genNamespace, serviceName, serializerSubNamespace, serializerClassName, reqSchemaClass, respSchemaClass, normalizedVersionSuffix);
                    break;
                default:
                    throw GetLanguageNotRecognizedException(language);
            }

            cmdNameReqResps.Add((commandName, reqSchemaClass, respSchemaClass));
        }

        private static IEnumerable<ITemplateTransform> GetServiceTransforms(string language, string projectName, string genNamespace, string modelId, string serviceName, string genFormat, string? commandTopic, string? telemetryTopic, string? cmdServiceGroupId, string? telemServiceGroupId, List<(string, string?, string?)> cmdNameReqResps, List<string> telemSchemas, HashSet<string> sourceFilePaths, bool syncApi)
        {
            bool doesCommandTargetExecutor = DoesTopicReferToExecutor(commandTopic);
            bool doesCommandTargetService = DoesTopicReferToService(commandTopic);
            bool doesTelemetryTargetService = DoesTopicReferToService(telemetryTopic);
            string serializerSubNamespace = formatSerializers[genFormat].SubNamespace;
            string serializerEmptyType = formatSerializers[genFormat].EmptyType;

            switch (language)
            {
                case "csharp":
                    yield return new DotNetService(projectName, genNamespace, serviceName, serializerSubNamespace, serializerEmptyType, commandTopic, telemetryTopic, cmdServiceGroupId, telemServiceGroupId, cmdNameReqResps, telemSchemas, doesCommandTargetExecutor, doesCommandTargetService, doesTelemetryTargetService, syncApi);
                    break;
                case "go":
                    yield return new GoService(genNamespace, modelId, serviceName, commandTopic, telemetryTopic, cmdServiceGroupId, telemServiceGroupId, cmdNameReqResps, telemSchemas, doesCommandTargetService, doesTelemetryTargetService, syncApi);
                    break;
                case "java":
                    yield return new JavaService(genNamespace, modelId, serviceName, commandTopic, telemetryTopic, cmdServiceGroupId, telemServiceGroupId, cmdNameReqResps, telemSchemas, doesCommandTargetExecutor, doesCommandTargetService, doesTelemetryTargetService);
                    break;
                case "python":
                    yield return new PythonService(genNamespace, modelId, serviceName, commandTopic, telemetryTopic, cmdServiceGroupId, telemServiceGroupId, cmdNameReqResps, telemSchemas, doesCommandTargetExecutor, doesCommandTargetService, doesTelemetryTargetService);
                    break;
                case "rust":
                    yield return new RustService(genNamespace, modelId, serviceName, commandTopic, telemetryTopic, cmdServiceGroupId, telemServiceGroupId, cmdNameReqResps, telemSchemas, doesCommandTargetExecutor, doesCommandTargetService, doesTelemetryTargetService, sourceFilePaths);
                    break;
                case "c":
                    break;
                default:
                    throw GetLanguageNotRecognizedException(language);
            }
        }

        private static IEnumerable<ITemplateTransform> GetProjectTransforms(string language, string projectName, string genNamespace, string genFormat, string? sdkPath, HashSet<string> sourceFilePaths, List<string> schemaTypes)
        {
            switch (language)
            {
                case "csharp":
                    yield return new DotNetProject(projectName, genFormat, sdkPath);
                    break;
                case "go":
                    break;
                case "java":
                    break;
                case "python":
                    break;
                case "rust":
                    yield return new RustLib(genNamespace);
                    yield return new RustCargoToml(projectName, genFormat, sdkPath);
                    break;
                case "c":
                    break;
                default:
                    throw GetLanguageNotRecognizedException(language);
            }
        }

        private static IEnumerable<ITemplateTransform> GetResourceTransforms(string language, string projectName, string genFormat)
        {
            string serializerSubNamespace = formatSerializers[genFormat].SubNamespace;

            string ext = language switch
            {
                "csharp" => "cs",
                "go" => "go",
                "java" => "java",
                "python" => "py",
                "rust" => "rs",
                "c" => "c",
                _ => throw GetLanguageNotRecognizedException(language)
            };

            foreach (string subNamespace in new List<string> { "common", serializerSubNamespace })
            {
                foreach (string resourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                {
                    Regex rx = new($"^{Assembly.GetExecutingAssembly().GetName().Name}\\.{ResourceNames.LanguageResourcesFolder}\\.{language}\\.({subNamespace})(?:\\.(\\w+(?:\\.\\w+)*))?\\.(\\w+)\\.{ext}$", RegexOptions.IgnoreCase);
                    Match? match = rx.Match(resourceName);
                    if (match.Success)
                    {
                        string subFolder = match.Groups[1].Captures[0].Value;
                        string resourcePath = match.Groups[2].Captures.Count > 0 ? match.Groups[2].Captures[0].Value : string.Empty;
                        string resourceFile = match.Groups[3].Captures[0].Value;

                        StreamReader resourceReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!);

                        yield return new ResourceTransform(language, projectName, subFolder, resourcePath, resourceFile, ext, resourceReader.ReadToEnd());
                    }
                }
            }
        }

        private static bool DoesTopicReferToExecutor(string? topic)
        {
            return topic != null && topic.Contains(MqttTopicTokens.CommandExecutorId);
        }

        private static bool DoesTopicReferToService(string? topic)
        {
            return topic != null && topic.Contains(MqttTopicTokens.ModelId);
        }

        private static Exception GetLanguageNotRecognizedException(string language)
        {
            return new Exception($"language '{language}' not recognized");
        }

        private readonly struct SerializerValues
        {
            public SerializerValues(string subNamespace, string className, string emptyType)
            {
                SubNamespace = subNamespace;
                ClassName = className;
                EmptyType = emptyType;
            }

            public readonly string SubNamespace;
            public readonly string ClassName;
            public readonly string EmptyType;
        }
    }
}
