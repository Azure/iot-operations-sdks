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
            { PayloadFormat.Avro, new SerializerValues("AVRO", "AvroSerializer{0}", EmptyTypeName.AvroInstance) },
            { PayloadFormat.Cbor, new SerializerValues("CBOR", "CborSerializer", EmptyTypeName.CborInstance) },
            { PayloadFormat.Json, new SerializerValues("JSON", "Utf8JsonSerializer", EmptyTypeName.JsonInstance) },
            { PayloadFormat.Proto2, new SerializerValues("protobuf", "ProtobufSerializer{0}", EmptyTypeName.ProtoInstance) },
            { PayloadFormat.Proto3, new SerializerValues("protobuf", "ProtobufSerializer{0}", EmptyTypeName.ProtoInstance) },
            { PayloadFormat.Raw, new SerializerValues("raw", "PassthroughSerializer", EmptyTypeName.RawInstance) },
            { PayloadFormat.Custom, new SerializerValues("custom", "ExternalSerializer", EmptyTypeName.CustomInstance) },
        };

        public static IEnumerable<ITemplateTransform> GetTransforms(string language, string projectName, JsonDocument annexDocument, string? workingPath, string? sdkPath, bool generateClient, bool generateServer, bool defaultImpl, string genRoot, CodeName? sharedPrefix, bool generateProject)
        {
            string modelId = annexDocument.RootElement.GetProperty(AnnexFileProperties.ModelId).GetString()!;
            CodeName genNamespace = new CodeName(annexDocument.RootElement.GetProperty(AnnexFileProperties.Namespace).GetString()!);
            CodeName? sharedNamespace = annexDocument.RootElement.TryGetProperty(AnnexFileProperties.Shared, out JsonElement sharedElt) ? new(sharedElt.GetString()!) : null;
            CodeName serviceName = new CodeName(annexDocument.RootElement.GetProperty(AnnexFileProperties.ServiceName).GetString()!);
            string genFormat = annexDocument.RootElement.GetProperty(AnnexFileProperties.PayloadFormat).GetString()!;
            bool separateTelemetries = annexDocument.RootElement.GetProperty(AnnexFileProperties.TelemSeparate).GetBoolean();

            string? telemetryTopic = annexDocument.RootElement.TryGetProperty(AnnexFileProperties.TelemetryTopic, out JsonElement telemTopicElt) ? telemTopicElt.GetString() : null;
            string? commandTopic = annexDocument.RootElement.TryGetProperty(AnnexFileProperties.CommandRequestTopic, out JsonElement cmdTopicElt) ? cmdTopicElt.GetString() : null;
            string? telemServiceGroupId = annexDocument.RootElement.TryGetProperty(AnnexFileProperties.TelemServiceGroupId, out JsonElement tGroupIdElt) ? tGroupIdElt.GetString() : null;
            string? cmdServiceGroupId = annexDocument.RootElement.TryGetProperty(AnnexFileProperties.CmdServiceGroupId, out JsonElement cGroupIdElt) ? cGroupIdElt.GetString() : null;

            string? version = modelId.IndexOf(";") > 0 ? modelId.Substring(modelId.IndexOf(";") + 1) : null;
            string? normalizedVersionSuffix = version?.Replace(".", "_");

            List<CommandEnvoyInfo> cmdEnvoyInfos = new();
            List<TelemetryEnvoyInfo> telemEnvoyInfos = new();

            if (annexDocument.RootElement.TryGetProperty(AnnexFileProperties.TelemetryList, out JsonElement telemsElt) && telemsElt.GetArrayLength() > 0)
            {
                if (telemetryTopic == null)
                {
                    throw new Exception($"Model {modelId} has at least one Telemetry content but no {DtdlMqttExtensionValues.GetStandardTerm(DtdlMqttExtensionValues.TelemTopicPropertyFormat)} property");
                }

                foreach (JsonElement telemEl in telemsElt.EnumerateArray())
                {
                    foreach (ITemplateTransform templateTransform in GetTelemetryTransforms(language, projectName, genNamespace, modelId, serviceName, genFormat, telemEl, telemEnvoyInfos, workingPath, generateClient, generateServer, useSharedSubscription: telemServiceGroupId != null))
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
                    foreach (ITemplateTransform templateTransform in GetCommandTransforms(language, projectName, genNamespace, modelId, serviceName, genFormat, commandTopic, cmdEl, cmdEnvoyInfos, normalizedVersionSuffix, workingPath, generateClient, generateServer, useSharedSubscription: cmdServiceGroupId != null))
                    {
                        yield return templateTransform;
                    }
                }
            }

            if (annexDocument.RootElement.TryGetProperty(AnnexFileProperties.ErrorList, out JsonElement errsElt) && errsElt.GetArrayLength() > 0)
            {
                foreach (JsonElement errEl in errsElt.EnumerateArray())
                {
                    foreach (ITemplateTransform templateTranform in GetErrorTransforms(language, projectName, genNamespace, errEl))
                    {
                        yield return templateTranform;
                    }
                }
            }

            foreach (ITemplateTransform templateTransform in GetServiceTransforms(language, projectName, genNamespace, sharedNamespace, modelId, serviceName, genFormat, commandTopic, telemetryTopic, cmdServiceGroupId, telemServiceGroupId, cmdEnvoyInfos, telemEnvoyInfos, sharedPrefix, genRoot, generateClient, generateServer, defaultImpl, separateTelemetries))
            {
                yield return templateTransform;
            }

            foreach (ITemplateTransform templateTransform in GetResourceTransforms(language, projectName, genFormat))
            {
                yield return templateTransform;
            }

            foreach (ITemplateTransform templateTransform in GetProjectTransforms(language, projectName, genNamespace, genFormat, sdkPath, sharedPrefix, generateProject))
            {
                yield return templateTransform;
            }
        }

        private static IEnumerable<ITemplateTransform> GetTelemetryTransforms(string language, string projectName, CodeName genNamespace, string modelId, CodeName serviceName, string genFormat, JsonElement telemElt, List<TelemetryEnvoyInfo> telemEnvoyInfos, string? workingPath, bool generateClient, bool generateServer, bool useSharedSubscription)
        {
            string serializerSubNamespace = formatSerializers[genFormat].SubNamespace;
            string serializerClassName = formatSerializers[genFormat].ClassName;
            EmptyTypeName serializerEmptyType = formatSerializers[genFormat].EmptyType;

            CodeName telemetryName = new CodeName(telemElt.TryGetProperty(AnnexFileProperties.TelemName, out JsonElement nameElt) ? nameElt.GetString() ?? string.Empty : string.Empty);
            string schemaRep = telemElt.GetProperty(AnnexFileProperties.TelemSchema).GetString()!;
            ITypeName schemaType = schemaRep switch
            {
                RawTypeName.Designator => RawTypeName.Instance,
                CustomTypeName.Designator => CustomTypeName.Instance,
                _ => new CodeName(schemaRep),
            };

            switch (language)
            {
                case "csharp":
                    if (generateServer)
                    {
                        yield return new DotNetTelemetrySender(telemetryName, projectName, genNamespace, modelId, serviceName, serializerSubNamespace, serializerClassName, serializerEmptyType, schemaType);
                    }

                    if (generateClient)
                    {
                        yield return new DotNetTelemetryReceiver(telemetryName, projectName, genNamespace, modelId, serviceName, serializerSubNamespace, serializerClassName, serializerEmptyType, schemaType);
                    }

                    if (genFormat == PayloadFormat.Avro && schemaType is CodeName dotnetSchema)
                    {
                        yield return new DotNetSerialization(projectName, genNamespace, dotnetSchema, workingPath);
                    }

                    break;
                case "go":
                    if (generateServer)
                    {
                        yield return new GoTelemetrySender(telemetryName, genNamespace, serializerSubNamespace, schemaType);
                    }

                    if (generateClient)
                    {
                        yield return new GoTelemetryReceiver(telemetryName, genNamespace, serializerSubNamespace, schemaType);
                    }

                    break;
                case "java":
                    if (generateServer)
                    {
                        yield return new JavaTelemetrySender(telemetryName.AsGiven, genNamespace, serializerSubNamespace, serializerClassName, schemaType.GetTypeName(TargetLanguage.Java));
                    }

                    if (generateClient)
                    {
                        yield return new JavaTelemetryReceiver(telemetryName.AsGiven, genNamespace, serializerSubNamespace, serializerClassName, schemaType.GetTypeName(TargetLanguage.Java));
                    }

                    break;
                case "python":
                    if (generateServer)
                    {
                        yield return new PythonTelemetrySender(telemetryName.AsGiven, genNamespace, serializerSubNamespace, serializerClassName, schemaType.GetTypeName(TargetLanguage.Python));
                    }

                    if (generateClient)
                    {
                        yield return new PythonTelemetryReceiver(telemetryName.AsGiven, genNamespace, serializerSubNamespace, serializerClassName, schemaType.GetTypeName(TargetLanguage.Python));
                    }

                    break;
                case "rust":
                    if (generateServer)
                    {
                        yield return new RustTelemetrySender(telemetryName, genNamespace, schemaType);
                    }

                    if (generateClient)
                    {
                        yield return new RustTelemetryReceiver(telemetryName, genNamespace, schemaType, useSharedSubscription);
                    }

                    if (schemaType is CodeName rustSchema)
                    {
                        yield return new RustSerialization(genNamespace, genFormat, rustSchema, workingPath);
                    }

                    break;
                case "c":
                    break;
                default:
                    throw GetLanguageNotRecognizedException(language);
            }

            telemEnvoyInfos.Add(new TelemetryEnvoyInfo(telemetryName, schemaType));
        }

        private static IEnumerable<ITemplateTransform> GetCommandTransforms(string language, string projectName, CodeName genNamespace, string modelId, CodeName serviceName, string genFormat, string? commandTopic, JsonElement cmdElt, List<CommandEnvoyInfo> cmdEnvoyInfos, string? normalizedVersionSuffix, string? workingPath, bool generateClient, bool generateServer, bool useSharedSubscription)
        {
            bool doesCommandTargetExecutor = DoesTopicReferToExecutor(commandTopic);
            string serializerSubNamespace = formatSerializers[genFormat].SubNamespace;
            string serializerClassName = formatSerializers[genFormat].ClassName;
            EmptyTypeName serializerEmptyType = formatSerializers[genFormat].EmptyType;

            CodeName commandName = new CodeName(cmdElt.GetProperty(AnnexFileProperties.CommandName).GetString()!);

            string? reqSchemaRep = cmdElt.TryGetProperty(AnnexFileProperties.CmdRequestSchema, out JsonElement reqSchemaElt) ? reqSchemaElt.GetString() : null;
            ITypeName? reqSchemaType = reqSchemaRep == null ? null : reqSchemaRep switch
            {
                RawTypeName.Designator => RawTypeName.Instance,
                CustomTypeName.Designator => CustomTypeName.Instance,
                _ => new CodeName(reqSchemaRep),
            };
            CodeName? reqSchemaNamespace = cmdElt.TryGetProperty(AnnexFileProperties.CmdRequestNamespace, out JsonElement reqNamespaceElt) ? new(reqNamespaceElt.GetString()!) : null;

            string? respSchemaRep = cmdElt.TryGetProperty(AnnexFileProperties.CmdResponseSchema, out JsonElement respSchemaElt) ? respSchemaElt.GetString() : null;
            ITypeName? respSchemaType = respSchemaRep == null ? null : reqSchemaRep switch
            {
                RawTypeName.Designator => RawTypeName.Instance,
                CustomTypeName.Designator => CustomTypeName.Instance,
                _ => new CodeName(respSchemaRep),
            };
            CodeName? respSchemaNamespace = cmdElt.TryGetProperty(AnnexFileProperties.CmdResponseNamespace, out JsonElement respNamespaceElt) ? new(respNamespaceElt.GetString()!) : null;

            CodeName? normalResultName = cmdElt.TryGetProperty(AnnexFileProperties.NormalResultName, out JsonElement normalNameElt) ? new(normalNameElt.GetString()!) : null;
            CodeName? normalResultSchema = cmdElt.TryGetProperty(AnnexFileProperties.NormalResultSchema, out JsonElement normalSchemaElt) ? new(normalSchemaElt.GetString()!) : null;
            CodeName? normalResultNamespace = cmdElt.TryGetProperty(AnnexFileProperties.NormalResultNamespace, out JsonElement normalNamespaceElt) ? new(normalNamespaceElt.GetString()!) : null;
            CodeName? errorResultName = cmdElt.TryGetProperty(AnnexFileProperties.ErrorResultName, out JsonElement errorNameElt) ? new(errorNameElt.GetString()!) : null;
            CodeName? errorResultSchema = cmdElt.TryGetProperty(AnnexFileProperties.ErrorResultSchema, out JsonElement errorSchemaElt) ? new(errorSchemaElt.GetString()!) : null;
            CodeName? errorResultNamespace = cmdElt.TryGetProperty(AnnexFileProperties.ErrorResultNamespace, out JsonElement errorNamespaceElt) ? new(errorNamespaceElt.GetString()!) : null;
            bool isRequestNullable = cmdElt.TryGetProperty(AnnexFileProperties.RequestIsNullable, out JsonElement reqNullableElt) ? reqNullableElt.GetBoolean() : false;
            bool isResponseNullable = cmdElt.TryGetProperty(AnnexFileProperties.ResponseIsNullable, out JsonElement respNullableElt) ? respNullableElt.GetBoolean() : false;

            bool isIdempotent = cmdElt.GetProperty(AnnexFileProperties.CmdIsIdempotent).GetBoolean();
            string? cacheability = cmdElt.GetProperty(AnnexFileProperties.Cacheability).GetString();

            switch (language)
            {
                case "csharp":
                    if (generateClient)
                    {
                        yield return new DotNetCommandInvoker(commandName, projectName, genNamespace, modelId, serviceName, serializerSubNamespace, serializerClassName, serializerEmptyType, reqSchemaType, respSchemaType, reqSchemaNamespace, respSchemaNamespace);
                    }

                    if (generateServer)
                    {
                        yield return new DotNetCommandExecutor(commandName, projectName, genNamespace, modelId, serviceName, serializerSubNamespace, serializerClassName, serializerEmptyType, reqSchemaType, respSchemaType, reqSchemaNamespace, respSchemaNamespace, isIdempotent, cacheability);
                    }

                    if (genFormat == PayloadFormat.Avro)
                    {
                        if (reqSchemaType is CodeName dotnetReqSchema)
                        {
                            yield return new DotNetSerialization(projectName, genNamespace, dotnetReqSchema, workingPath);
                        }

                        if (respSchemaType is CodeName dotnetRespSchema)
                        {
                            yield return new DotNetSerialization(projectName, genNamespace, dotnetRespSchema, workingPath);
                        }
                    }

                    break;
                case "go":
                    if (generateClient)
                    {
                        yield return new GoCommandInvoker(commandName, genNamespace, serializerSubNamespace, reqSchemaType, respSchemaType, normalResultName, normalResultSchema, errorResultName, errorResultSchema, isResponseNullable, doesCommandTargetExecutor);
                    }

                    if (generateServer)
                    {
                        yield return new GoCommandExecutor(commandName, genNamespace, serializerSubNamespace, reqSchemaType, respSchemaType, normalResultName, normalResultSchema, errorResultName, errorResultSchema, isResponseNullable, isIdempotent, cacheability);
                    }

                    break;
                case "java":
                    if (generateClient)
                    {
                        yield return new JavaCommandInvoker(commandName, genNamespace, serializerSubNamespace, serializerClassName, reqSchemaType?.GetTypeName(TargetLanguage.Java), respSchemaType?.GetTypeName(TargetLanguage.Java));
                    }

                    if (generateServer)
                    {
                        yield return new JavaCommandExecutor(commandName, genNamespace, serializerSubNamespace, serializerClassName, reqSchemaType?.GetTypeName(TargetLanguage.Java), respSchemaType?.GetTypeName(TargetLanguage.Java));
                    }

                    break;
                case "python":
                    if (generateClient)
                    {
                        yield return new PythonCommandInvoker(commandName, genNamespace, serializerSubNamespace, serializerClassName, reqSchemaType?.GetTypeName(TargetLanguage.Python), respSchemaType?.GetTypeName(TargetLanguage.Python));
                    }

                    if (generateServer)
                    {
                        yield return new PythonCommandExecutor(commandName, genNamespace, serializerSubNamespace, serializerClassName, reqSchemaType?.GetTypeName(TargetLanguage.Python), respSchemaType?.GetTypeName(TargetLanguage.Python));
                    }

                    break;
                case "rust":
                    if (generateClient)
                    {
                        yield return new RustCommandInvoker(commandName, genNamespace, serializerEmptyType, reqSchemaType, respSchemaType, reqSchemaNamespace, respSchemaNamespace, normalResultName, normalResultSchema, normalResultNamespace, errorResultName, errorResultSchema, errorResultNamespace, isResponseNullable, doesCommandTargetExecutor);
                    }

                    if (generateServer)
                    {
                        yield return new RustCommandExecutor(commandName, genNamespace, serializerEmptyType, reqSchemaType, respSchemaType, reqSchemaNamespace, respSchemaNamespace, normalResultName, normalResultSchema, normalResultNamespace, errorResultName, errorResultSchema, errorResultNamespace, isResponseNullable, isIdempotent, cacheability, useSharedSubscription);
                    }

                    if (reqSchemaType is CodeName rustReqSchema)
                    {
                        yield return new RustSerialization(reqSchemaNamespace ?? genNamespace, genFormat, rustReqSchema, workingPath);
                    }

                    if (respSchemaType is CodeName rustRespSchema)
                    {
                        yield return new RustSerialization(respSchemaNamespace ?? genNamespace, genFormat, rustRespSchema, workingPath);
                    }

                    if (normalResultSchema != null)
                    {
                        yield return new RustSerialization(respSchemaNamespace ?? genNamespace, genFormat, normalResultSchema, workingPath);
                    }

                    break;
                case "c":
                    if (generateClient)
                    {
                        yield return new CCommandInvoker(modelId, commandName, commandTopic!, genNamespace, serviceName, serializerSubNamespace, serializerClassName, reqSchemaType?.GetTypeName(TargetLanguage.Independent), respSchemaType?.GetTypeName(TargetLanguage.Independent), normalizedVersionSuffix);
                    }

                    if (generateServer)
                    {
                        yield return new CCommandExecutor(modelId, commandName, commandTopic!, genNamespace, serviceName, serializerSubNamespace, serializerClassName, reqSchemaType?.GetTypeName(TargetLanguage.Independent), respSchemaType?.GetTypeName(TargetLanguage.Independent), normalizedVersionSuffix);
                    }

                    break;
                default:
                    throw GetLanguageNotRecognizedException(language);
            }

            cmdEnvoyInfos.Add(new CommandEnvoyInfo(commandName, reqSchemaType, respSchemaType, normalResultName, normalResultSchema, errorResultName, errorResultSchema, isRequestNullable, isResponseNullable));
        }

        private static IEnumerable<ITemplateTransform> GetErrorTransforms(string language, string projectName, CodeName genNamespace, JsonElement errElt)
        {
            CodeName schemaName = new CodeName(errElt.GetProperty(AnnexFileProperties.ErrorSchema).GetString()!);
            CodeName schemaNamespace = errElt.TryGetProperty(AnnexFileProperties.ErrorNamespace, out JsonElement namespaceElt) ? new CodeName(namespaceElt.GetString()!) : genNamespace;
            string description = errElt.TryGetProperty(AnnexFileProperties.ErrorDescription, out JsonElement descriptionElt) ? descriptionElt.GetString() ?? schemaName.AsGiven : schemaName.AsGiven;
            CodeName? messageField = errElt.TryGetProperty(AnnexFileProperties.ErrorMessageField, out JsonElement msgFieldElt) ? new CodeName(msgFieldElt.GetString()!) : null;
            bool isNullable = errElt.TryGetProperty(AnnexFileProperties.ErrorMessageIsNullable, out JsonElement nullableElt) ? nullableElt.GetBoolean() : false;

            switch (language)
            {
                case "csharp":
                    yield return new DotNetError(projectName, schemaName, schemaNamespace, description, messageField, isNullable);
                    break;
                case "go":
                    yield return new GoError(schemaName, schemaNamespace, description, messageField, isNullable);
                    break;
                case "java":
                    break;
                case "python":
                    break;
                case "rust":
                    yield return new RustError(schemaName, schemaNamespace, description, messageField, isNullable);
                    break;
                case "c":
                    break;
                default:
                    throw GetLanguageNotRecognizedException(language);
            }
        }

        private static IEnumerable<ITemplateTransform> GetServiceTransforms(string language, string projectName, CodeName genNamespace, CodeName? sharedNamespace, string modelId, CodeName serviceName, string genFormat, string? commandTopic, string? telemetryTopic, string? cmdServiceGroupId, string? telemServiceGroupId, List<CommandEnvoyInfo> cmdEnvoyInfos, List<TelemetryEnvoyInfo> telemEnvoyInfos, CodeName? sharedPrefix, string genRoot, bool generateClient, bool generateServer, bool defaultImpl, bool separateTelemetries)
        {
            bool doesCommandTargetExecutor = DoesTopicReferToExecutor(commandTopic);
            bool doesCommandTargetService = DoesTopicReferToService(commandTopic);
            bool doesTelemetryTargetService = DoesTopicReferToService(telemetryTopic);
            string serializerSubNamespace = formatSerializers[genFormat].SubNamespace;
            EmptyTypeName serializerEmptyType = formatSerializers[genFormat].EmptyType;

            switch (language)
            {
                case "csharp":
                    yield return new DotNetService(projectName, genNamespace, sharedNamespace, serviceName, serializerSubNamespace, serializerEmptyType, commandTopic, telemetryTopic, cmdServiceGroupId, telemServiceGroupId, cmdEnvoyInfos, telemEnvoyInfos, doesCommandTargetExecutor, doesCommandTargetService, doesTelemetryTargetService, generateClient, generateServer, defaultImpl);
                    break;
                case "go":
                    yield return new GoService(genNamespace, modelId, serviceName, commandTopic, telemetryTopic, cmdServiceGroupId, telemServiceGroupId, cmdEnvoyInfos, telemEnvoyInfos, doesCommandTargetService, doesTelemetryTargetService, generateClient, generateServer, separateTelemetries);
                    break;
                case "java":
                    yield return new JavaService(genNamespace, modelId, serviceName, commandTopic, telemetryTopic, cmdServiceGroupId, telemServiceGroupId, cmdEnvoyInfos, telemEnvoyInfos, doesCommandTargetExecutor, doesCommandTargetService, doesTelemetryTargetService);
                    break;
                case "python":
                    yield return new PythonService(genNamespace, modelId, serviceName, commandTopic, telemetryTopic, cmdServiceGroupId, telemServiceGroupId, cmdEnvoyInfos, telemEnvoyInfos, doesCommandTargetExecutor, doesCommandTargetService, doesTelemetryTargetService);
                    break;
                case "rust":
                    yield return new RustService(genNamespace, modelId, commandTopic, telemetryTopic, cmdServiceGroupId, telemServiceGroupId, generateClient, generateServer, genRoot);
                    if (sharedPrefix != null)
                    {
                        yield return new RustShared(sharedPrefix, genRoot);
                    }
                    break;
                case "c":
                    break;
                default:
                    throw GetLanguageNotRecognizedException(language);
            }
        }

        private static IEnumerable<ITemplateTransform> GetProjectTransforms(string language, string projectName, CodeName genNamespace, string genFormat, string? sdkPath, CodeName? sharedPrefix, bool generateProject)
        {
            switch (language)
            {
                case "csharp":
                    if (generateProject)
                    {
                        yield return new DotNetProject(projectName, genFormat, sdkPath);
                    }
                    break;
                case "go":
                    break;
                case "java":
                    break;
                case "python":
                    break;
                case "rust":
                    yield return new RustLib(genNamespace, sharedPrefix, generateProject);
                    yield return new RustCargoToml(projectName, genFormat, sdkPath, generateProject);
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
            public SerializerValues(string subNamespace, string className, EmptyTypeName emptyType)
            {
                SubNamespace = subNamespace;
                ClassName = className;
                EmptyType = emptyType;
            }

            public readonly string SubNamespace;
            public readonly string ClassName;
            public readonly EmptyTypeName EmptyType;
        }
    }
}
