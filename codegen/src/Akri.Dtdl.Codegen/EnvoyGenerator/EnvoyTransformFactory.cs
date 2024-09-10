namespace Akri.Dtdl.Codegen
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    public static class EnvoyTransformFactory
    {
        private static readonly string[] SupportedLanguages = { "csharp", "go", "java", "python", "c" };

        private static readonly Dictionary<string, SerializerValues> formatSerializers = new()
        {
            { PayloadFormat.Avro, new SerializerValues("AVRO", "AvroSerializer{0}", "EmptyAvro", isSchematized: true) },
            { PayloadFormat.Cbor, new SerializerValues("CBOR", "CborSerializer", "EmptyCbor", isSchematized: false) },
            { PayloadFormat.Json, new SerializerValues("JSON", "Utf8JsonSerializer", "EmptyJson", isSchematized: false) },
            { PayloadFormat.Proto2, new SerializerValues("protobuf", "ProtobufSerializer{0}", "Google.Protobuf.WellKnownTypes.Empty", isSchematized: true) },
            { PayloadFormat.Proto3, new SerializerValues("protobuf", "ProtobufSerializer{0}", "Google.Protobuf.WellKnownTypes.Empty", isSchematized: true) },
            { PayloadFormat.Raw, new SerializerValues("raw", "PassthroughSerializer", "", isSchematized: false) },
        };

        public static IEnumerable<ITemplateTransform> GetTransforms(string language, string projectName, JsonDocument annexDocument, string? workingPath, string? sdkPath, bool syncApi, HashSet<string> sourceFilePaths)
        {
            string modelId = annexDocument.RootElement.GetProperty(AnnexFileProperties.ModelId).GetString()!;
            string genNamespace = annexDocument.RootElement.GetProperty(AnnexFileProperties.Namespace).GetString()!;
            string serviceName = annexDocument.RootElement.GetProperty(AnnexFileProperties.ServiceName).GetString()!;
            string genFormat = annexDocument.RootElement.GetProperty(AnnexFileProperties.PayloadFormat).GetString()!;

            string? telemetryTopic = annexDocument.RootElement.TryGetProperty(AnnexFileProperties.TelemetryTopic, out JsonElement telemTopcElt) ? telemTopcElt.GetString() : null;
            string? commandTopic = annexDocument.RootElement.TryGetProperty(AnnexFileProperties.CommandRequestTopic, out JsonElement cmdTopcElt) ? cmdTopcElt.GetString() : null;

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
                    foreach (ITemplateTransform templateTransform in GetTelemetryTransforms(language, projectName, genNamespace, serviceName, genFormat, telemEl, telemSchemas, workingPath, schemaTypes))
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
                    foreach (ITemplateTransform templateTransform in GetCommandTransforms(modelId, language, projectName, genNamespace, serviceName, genFormat, commandTopic, cmdEl, cmdNameReqResps, commandTopic, normalizedVersionSuffix, workingPath, schemaTypes))
                    {
                        yield return templateTransform;
                    }
                }
            }

            foreach (ITemplateTransform templateTransform in GetServiceTransforms(language, projectName, genNamespace, modelId, serviceName, genFormat, commandTopic, telemetryTopic, cmdNameReqResps, telemSchemas, syncApi))
            {
                yield return templateTransform;
            }

            foreach (ITemplateTransform templateTransform in GetSerializerTransforms(language, projectName, genFormat))
            {
                yield return templateTransform;
            }

            foreach (ITemplateTransform templateTransform in GetProjectTransforms(language, projectName, genNamespace, genFormat, sdkPath, sourceFilePaths, schemaTypes))
            {
                yield return templateTransform;
            }
        }

        private static IEnumerable<ITemplateTransform> GetTelemetryTransforms(string language, string projectName, string genNamespace, string serviceName, string genFormat, JsonElement telemElt, List<string> telemSchemas, string? workingPath, List<string> schemaTypes)
        {
            string serializerSubNamespace = formatSerializers[genFormat].SubNamespace;
            string serializerlClassName = formatSerializers[genFormat].ClassName;
            string serialzerEmptyType = formatSerializers[genFormat].EmptyType;

            string? telemetryName = telemElt.TryGetProperty(AnnexFileProperties.TelemName, out JsonElement nameElt) ? nameElt.GetString() : null;
            string schemaClass = telemElt.GetProperty(AnnexFileProperties.TelemSchema).GetString()!;

            switch (language)
            {
                case "csharp":
                    yield return new DotNetTelemetrySender(telemetryName, projectName, genNamespace, serviceName, serializerSubNamespace, serializerlClassName, serialzerEmptyType, schemaClass);
                    yield return new DotNetTelemetryReceiver(telemetryName, projectName, genNamespace, serviceName, serializerSubNamespace, serializerlClassName, serialzerEmptyType, schemaClass);
                    break;
                case "go":
                    yield return new GoTelemetrySender(telemetryName, genNamespace, serializerSubNamespace, schemaClass);
                    yield return new GoTelemetryReceiver(telemetryName, genNamespace, serializerSubNamespace, schemaClass);
                    break;
                case "java":
                    yield return new JavaTelemetrySender(telemetryName, genNamespace, serializerSubNamespace, serializerlClassName, schemaClass);
                    yield return new JavaTelemetryReceiver(telemetryName, genNamespace, serializerSubNamespace, serializerlClassName, schemaClass);
                    break;
                case "python":
                    yield return new PythonTelemetrySender(telemetryName, genNamespace, serializerSubNamespace, serializerlClassName, schemaClass);
                    yield return new PythonTelemetryReceiver(telemetryName, genNamespace, serializerSubNamespace, serializerlClassName, schemaClass);
                    break;
                case "rust":
                    yield return new RustTelemetrySender(telemetryName, genNamespace, serializerSubNamespace, serializerlClassName, schemaClass);
                    yield return new RustTelemetryReceiver(telemetryName, genNamespace, serializerSubNamespace, serializerlClassName, schemaClass);
                    if (schemaClass != string.Empty)
                    {
                        schemaTypes.Add(schemaClass);
                        yield return new RustSchema(genNamespace, schemaClass, workingPath);
                    }
                    break;
                case "c":
                    break;
                default:
                    throw GetLanguageNotRecognizedException(language);
            }

            telemSchemas.Add(schemaClass);
        }

        private static IEnumerable<ITemplateTransform> GetCommandTransforms(string modelId, string language, string projectName, string genNamespace, string serviceName, string genFormat, string? commandTopic, JsonElement cmdElt, List<(string, string?, string?)> cmdNameReqResps, string requestTopicName, string? normalizedVersionSuffix, string? workingPath, List<string> schemaTypes)
        {
            bool doesCommandTargetExecutor = DoesTopicReferToExecutor(commandTopic);
            string serializerSubNamespace = formatSerializers[genFormat].SubNamespace;
            string serializerlClassName = formatSerializers[genFormat].ClassName;
            string serialzerEmptyType = formatSerializers[genFormat].EmptyType;

            string commandName = cmdElt.GetProperty(AnnexFileProperties.CommandName).GetString()!;
            string? reqSchemaClass = cmdElt.GetProperty(AnnexFileProperties.CmdRequestSchema).GetString();
            string? respSchemaClass = cmdElt.GetProperty(AnnexFileProperties.CmdResponseSchema).GetString();
            bool isIdempotent = cmdElt.GetProperty(AnnexFileProperties.CmdIsIdempotent).GetBoolean();
            string? cacheability = cmdElt.GetProperty(AnnexFileProperties.Cacheability).GetString();

            switch (language)
            {
                case "csharp":
                    yield return new DotNetCommandInvoker(commandName, projectName, genNamespace, serviceName, serializerSubNamespace, serializerlClassName, serialzerEmptyType, reqSchemaClass, respSchemaClass);
                    yield return new DotNetCommandExecutor(commandName, projectName, genNamespace, serviceName, serializerSubNamespace, serializerlClassName, serialzerEmptyType, reqSchemaClass, respSchemaClass, isIdempotent, cacheability);
                    break;
                case "go":
                    yield return new GoCommandInvoker(commandName, genNamespace, serializerSubNamespace, reqSchemaClass, respSchemaClass, doesCommandTargetExecutor);
                    yield return new GoCommandExecutor(commandName, genNamespace, serializerSubNamespace, reqSchemaClass, respSchemaClass, isIdempotent, cacheability);
                    break;
                case "java":
                    yield return new JavaCommandInvoker(commandName, genNamespace, serializerSubNamespace, serializerlClassName, reqSchemaClass, respSchemaClass);
                    yield return new JavaCommandExecutor(commandName, genNamespace, serializerSubNamespace, serializerlClassName, reqSchemaClass, respSchemaClass);
                    break;
                case "python":
                    yield return new PythonCommandInvoker(commandName, genNamespace, serializerSubNamespace, serializerlClassName, reqSchemaClass, respSchemaClass);
                    yield return new PythonCommandExecutor(commandName, genNamespace, serializerSubNamespace, serializerlClassName, reqSchemaClass, respSchemaClass);
                    break;
                case "rust":
                    yield return new RustCommandInvoker(commandName, genNamespace, serializerSubNamespace, serializerlClassName, reqSchemaClass, respSchemaClass);
                    yield return new RustCommandExecutor(commandName, genNamespace, serializerSubNamespace, serializerlClassName, reqSchemaClass, respSchemaClass);
                    if (reqSchemaClass != null && reqSchemaClass != string.Empty)
                    {
                        schemaTypes.Add(reqSchemaClass);
                        yield return new RustSchema(genNamespace, reqSchemaClass, workingPath);
                    }
                    if (respSchemaClass != null && respSchemaClass != string.Empty)
                    {
                        schemaTypes.Add(respSchemaClass);
                        yield return new RustSchema(genNamespace, respSchemaClass, workingPath);
                    }
                    break;
                case "c":
                    yield return new CCommandInvoker(modelId, commandName, requestTopicName, genNamespace, serviceName, serializerSubNamespace, serializerlClassName, reqSchemaClass, respSchemaClass, normalizedVersionSuffix);
                    yield return new CCommandExecutor(modelId, commandName, requestTopicName, genNamespace, serviceName, serializerSubNamespace, serializerlClassName, reqSchemaClass, respSchemaClass, normalizedVersionSuffix);
                    break;
                default:
                    throw GetLanguageNotRecognizedException(language);
            }

            cmdNameReqResps.Add((commandName, reqSchemaClass, respSchemaClass));
        }

        private static IEnumerable<ITemplateTransform> GetServiceTransforms(string language, string projectName, string genNamespace, string modelId, string serviceName, string genFormat, string? commandTopic, string? telemetryTopic, List<(string, string?, string?)> cmdNameReqResps, List<string> telemSchemas, bool syncApi)
        {
            bool doesCommandTargetExecutor = DoesTopicReferToExecutor(commandTopic);
            bool doesCommandTargetService = DoesTopicReferToService(commandTopic);
            bool doesTelemetryTargetService = DoesTopicReferToService(telemetryTopic);
            string serializerSubNamespace = formatSerializers[genFormat].SubNamespace;
            string serialzerEmptyType = formatSerializers[genFormat].EmptyType;

            switch (language)
            {
                case "csharp":
                    yield return new DotNetService(projectName, genNamespace, modelId, serviceName, serializerSubNamespace, serialzerEmptyType, commandTopic, telemetryTopic, cmdNameReqResps, telemSchemas, doesCommandTargetExecutor, doesCommandTargetService, doesTelemetryTargetService, syncApi);
                    break;
                case "go":
                    yield return new GoService(genNamespace, modelId, serviceName, commandTopic, telemetryTopic, cmdNameReqResps, telemSchemas, doesCommandTargetService, doesTelemetryTargetService, syncApi);
                    break;
                case "java":
                    yield return new JavaService(genNamespace, modelId, serviceName, commandTopic, telemetryTopic, cmdNameReqResps, telemSchemas, doesCommandTargetExecutor, doesCommandTargetService, doesTelemetryTargetService);
                    break;
                case "python":
                    yield return new PythonService(genNamespace, modelId, serviceName, commandTopic, telemetryTopic, cmdNameReqResps, telemSchemas, doesCommandTargetExecutor, doesCommandTargetService, doesTelemetryTargetService);
                    break;
                case "rust":
                    yield return new RustService(genNamespace, modelId, serviceName, commandTopic, telemetryTopic, cmdNameReqResps, telemSchemas, doesCommandTargetExecutor, doesCommandTargetService, doesTelemetryTargetService);
                    break;
                case "c":
                    break;
                default:
                    throw GetLanguageNotRecognizedException(language);
            }
        }

        private static IEnumerable<ITemplateTransform> GetProjectTransforms(string language, string projectName, string genNamespace, string genFormat, string? sdkPath, HashSet<string> sourceFilePaths, List<string> schemaTypes)
        {
            string serializerSubNamespace = formatSerializers[genFormat].SubNamespace;
            bool isSchematized = formatSerializers[genFormat].IsSchematized;

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
                    yield return new RustNamespace(genNamespace, sourceFilePaths);
                    yield return new RustLib(genNamespace);
                    yield return new RustSerialization(serializerSubNamespace, isSchematized);
                    yield return new RustCargoToml(projectName, genFormat, sdkPath);
                    if (RustSchemata.TryCreate(genNamespace, genFormat, schemaTypes, out RustSchemata? rustSchemata))
                    {
                        yield return rustSchemata!;
                    }
                    break;
                case "c":
                    break;
                default:
                    throw GetLanguageNotRecognizedException(language);
            }
        }

        private static IEnumerable<ITemplateTransform> GetSerializerTransforms(string language, string projectName, string genFormat)
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

            foreach (string resourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                Regex rx = new($"^{Assembly.GetExecutingAssembly().GetName().Name}\\.{ResourceNames.SerializerFolder}\\.({serializerSubNamespace})(?:\\.(\\w+))?\\.{ext}$", RegexOptions.IgnoreCase);
                Match? match = rx.Match(resourceName);
                if (match.Success)
                {
                    string serializationFormat = match.Groups[1].Captures[0].Value;
                    string? serializationComponent = match.Groups[2].Captures.Count > 0 ? match.Groups[2].Captures[0].Value : null;

                    StreamReader resourceReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!);

                    yield return new SerializerTransform(language, projectName, serializationFormat, serializationComponent, ext, resourceReader.ReadToEnd());
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
            return new Exception($"language '{language}' not recognized; must be {string.Join(" or ", SupportedLanguages.Select(l => $"'{l}'"))}");
        }

        private readonly struct SerializerValues
        {
            public SerializerValues(string subNamespace, string className, string emptyType, bool isSchematized)
            {
                SubNamespace = subNamespace;
                ClassName = className;
                EmptyType = emptyType;
                IsSchematized = isSchematized;
            }

            public readonly string SubNamespace;
            public readonly string ClassName;
            public readonly string EmptyType;
            public readonly bool IsSchematized;
        }
    }
}
