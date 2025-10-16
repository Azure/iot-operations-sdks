namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Azure.Iot.Operations.CodeGeneration;

    internal class EnvoyTransformFactory
    {
        private static readonly Dictionary<SerializationFormat, SerializerValues> formatSerializers = new()
        {
            { SerializationFormat.Json, new SerializerValues("JSON", "Utf8JsonSerializer", EmptyTypeName.JsonInstance) },
        };

        private readonly TargetLanguage targetLanguage;
        private readonly CodeName genNamespace;
        private readonly string projectName;
        private readonly CodeName serviceName;
        private readonly string srcSubdir;
        private readonly bool generateClient;
        private readonly bool generateServer;

        internal EnvoyTransformFactory(
            TargetLanguage targetLanguage,
            CodeName genNamespace,
            string projectName,
            CodeName serviceName,
            string srcSubdir,
            bool generateClient,
            bool generateServer)
        {
            this.targetLanguage = targetLanguage;
            this.genNamespace = genNamespace;
            this.projectName = projectName;
            this.serviceName = serviceName;
            this.srcSubdir = srcSubdir;
            this.generateClient = generateClient;
            this.generateServer = generateServer;
        }

        internal IEnumerable<IEnvoyTemplateTransform> GetConstantTransforms(CodeName schemaName, List<TypedConstant> constants)
        {
            switch (targetLanguage)
            {
                case TargetLanguage.CSharp:
                    yield return new DotNetConstants(projectName, schemaName, genNamespace, constants);
                    break;
                case TargetLanguage.Rust:
                    yield return new RustConstants(schemaName, genNamespace, constants, srcSubdir);
                    break;
                default:
                    throw new NotSupportedException($"Target language {targetLanguage} is not supported.");
            }
        }

        internal IEnumerable<IEnvoyTemplateTransform> GetActionTransforms(
            SchemaNamer schemaNamer,
            string modelId,
            string actionName,
            string? inputSchemaType,
            string? outputSchemaType,
            SerializationFormat format,
            string? serviceGroupId,
            string topicPattern,
            bool idempotent,
            List<string> normalResultFields,
            List<string> normalRequiredFields,
            string? normalResultSchema,
            string? errorResultName,
            string? errorResultSchema,
            string? headerCodeName,
            string? headerCodeSchema,
            string? headerInfoName,
            string? headerInfoSchema,
            List<string>? codeValues)
        {
            bool doesCommandTargetExecutor = DoesTopicReferToExecutor(topicPattern);
            string serializerSubNamespace = formatSerializers[format].SubNamespace;
            string serializerClassName = formatSerializers[format].ClassName;
            EmptyTypeName serializerEmptyType = formatSerializers[format].EmptyType;

            CodeName commandName = new CodeName(actionName);

            ITypeName? inputSchema = inputSchemaType != null ? new CodeName(inputSchemaType) : null;
            ITypeName? outputSchema = outputSchemaType != null ? new CodeName(outputSchemaType) : null;

            List<CodeName> normalFields = normalResultFields.Select(f => new CodeName(f)).ToList();
            List<CodeName> requiredFields = normalRequiredFields.Select(f => new CodeName(f)).ToList();
            CodeName? normalSchema = normalResultSchema != null ? new CodeName(normalResultSchema) : null;
            CodeName? errorName = errorResultName != null ? new CodeName(errorResultName) : null;
            CodeName? errorSchema = errorResultSchema != null ? new CodeName(errorResultSchema) : null;

            CodeName? codeName = headerCodeName != null ? new CodeName(headerCodeName) : null;
            CodeName? codeSchema = headerCodeSchema != null ? new CodeName(headerCodeSchema) : null;
            CodeName? infoName = headerInfoName != null ? new CodeName(headerInfoName) : null;
            CodeName? infoSchema = headerInfoSchema != null ? new CodeName(headerInfoSchema) : null;

            switch (targetLanguage)
            {
                case TargetLanguage.CSharp:
                    if (generateServer)
                    {
                        yield return new DotNetCommandExecutor(
                            actionName,
                            schemaNamer.GetActionExecutorBinder(actionName),
                            projectName,
                            genNamespace,
                            modelId,
                            serviceName,
                            serializerClassName,
                            serializerEmptyType,
                            inputSchema,
                            outputSchema,
                            serviceGroupId,
                            topicPattern,
                            idempotent);
                    }

                    if (generateClient)
                    {
                        yield return new DotNetCommandInvoker(
                            actionName,
                            schemaNamer.GetActionInvokerBinder(actionName),
                            projectName,
                            genNamespace,
                            modelId,
                            serviceName,
                            serializerClassName,
                            serializerEmptyType,
                            inputSchema,
                            outputSchema,
                            topicPattern);
                    }

                    if (outputSchema != null && codeName != null && codeValues != null)
                    {
                        yield return new DotNetResponseExtension(
                            projectName,
                            genNamespace,
                            outputSchema,
                            codeName,
                            codeSchema!,
                            infoName,
                            infoSchema,
                            codeValues,
                            generateClient,
                            generateServer);
                    }

                    break;
                case TargetLanguage.Rust:
                    if (generateServer)
                    {
                        yield return new RustCommandExecutor(
                            actionName,
                            schemaNamer.GetActionExecutorBinder(actionName),
                            genNamespace,
                            modelId,
                            serializerEmptyType,
                            inputSchema,
                            outputSchema,
                            normalFields,
                            requiredFields,
                            normalSchema,
                            errorName,
                            errorSchema,
                            idempotent,
                            serviceGroupId,
                            topicPattern,
                            srcSubdir);
                        if (codeName != null && codeValues != null)
                        {
                            yield return new RustCommandExecutorHeaders(actionName, schemaNamer.GetActionExecutorBinder(actionName), genNamespace, codeName, codeSchema!, infoName, infoSchema, codeValues, srcSubdir);
                        }
                    }

                    if (generateClient)
                    {
                        yield return new RustCommandInvoker(
                            actionName,
                            schemaNamer.GetActionInvokerBinder(actionName),
                            genNamespace,
                            modelId,
                            serializerEmptyType,
                            inputSchema,
                            outputSchema,
                            normalFields,
                            requiredFields,
                            normalSchema,
                            errorName,
                            errorSchema,
                            topicPattern,
                            doesCommandTargetExecutor,
                            srcSubdir);
                        if (codeName != null && codeValues != null)
                        {
                            yield return new RustCommandInvokerHeaders(actionName, schemaNamer.GetActionInvokerBinder(actionName), genNamespace, codeName, codeSchema!, infoName, infoSchema, codeValues, srcSubdir);
                        }
                    }

                    break;
                default:
                    throw new NotSupportedException($"Target language {targetLanguage} is not supported.");
            }
        }

        internal IEnumerable<IEnvoyTemplateTransform> GetEventTransforms(
            SchemaNamer schemaNamer,
            string modelId,
            string eventName,
            string schemaType,
            SerializationFormat format,
            string? serviceGroupId,
            string topicPattern)
        {
            string serializerSubNamespace = formatSerializers[format].SubNamespace;
            string serializerClassName = formatSerializers[format].ClassName;
            EmptyTypeName serializerEmptyType = formatSerializers[format].EmptyType;

            switch (targetLanguage)
            {
                case TargetLanguage.CSharp:
                    if (generateServer)
                    {
                        yield return new DotNetTelemetrySender(eventName, schemaNamer.GetEventSenderBinder(eventName), projectName, genNamespace, modelId, serviceName, serializerSubNamespace, serializerClassName, serializerEmptyType, new CodeName(schemaType), topicPattern);
                    }

                    if (generateClient)
                    {
                        yield return new DotNetTelemetryReceiver(eventName, schemaNamer.GetEventReceiverBinder(eventName), projectName, genNamespace, modelId, serviceName, serializerSubNamespace, serializerClassName, serializerEmptyType, new CodeName(schemaType), serviceGroupId, topicPattern);
                    }

                    break;
                case TargetLanguage.Rust:
                    if (generateServer)
                    {
                        yield return new RustTelemetrySender(eventName, schemaNamer.GetEventSenderBinder(eventName), genNamespace, modelId, new CodeName(schemaType), topicPattern, srcSubdir);
                    }

                    if (generateClient)
                    {
                        yield return new RustTelemetryReceiver(eventName, schemaNamer.GetEventReceiverBinder(eventName), genNamespace, modelId, new CodeName(schemaType), serviceGroupId, topicPattern, srcSubdir);
                    }

                    break;
                default:
                    throw new NotSupportedException($"Target language {targetLanguage} is not supported."); 
            }
        }

        internal IEnumerable<IEnvoyTemplateTransform> GetErrorTransforms(ErrorSpec errorSpec)
        {
            switch (targetLanguage)
            {
                case TargetLanguage.CSharp:
                    yield return new DotNetError(
                        projectName,
                        new CodeName(errorSpec.SchemaName),
                        genNamespace,
                        errorSpec.ErrorCodeName != null ? new CodeName(errorSpec.ErrorCodeName) : null,
                        errorSpec.ErrorCodeSchema != null ? new CodeName(errorSpec.ErrorCodeSchema): null,
                        errorSpec.ErrorInfoName != null ? new CodeName(errorSpec.ErrorInfoName) : null,
                        errorSpec.ErrorInfoSchema != null ? new CodeName(errorSpec.ErrorInfoSchema) : null,
                        errorSpec.Description,
                        errorSpec.MessageField != null ? new CodeName(errorSpec.MessageField) : null,
                        errorSpec.MessageIsRequired);
                    break;
                case TargetLanguage.Rust:
                    yield return new RustError(
                        new CodeName(errorSpec.SchemaName),
                        genNamespace,
                        errorSpec.Description,
                        errorSpec.MessageField != null ? new CodeName(errorSpec.MessageField) : null,
                        errorSpec.MessageIsRequired,
                        srcSubdir);
                    break;
                default:
                    throw new NotSupportedException($"Target language {targetLanguage} is not supported.");
            }
        }

        internal IEnumerable<IEnvoyTemplateTransform> GetSerializationTransforms(string serializableType)
        {
            switch (targetLanguage)
            {
                case TargetLanguage.CSharp:
                    break;
                case TargetLanguage.Rust:
                    yield return new RustSerialization(
                        genNamespace,
                        SerializationFormat.Json,
                        new CodeName(serializableType),
                        srcSubdir);
                    break;
                default:
                    throw new NotSupportedException($"Target language {targetLanguage} is not supported.");
            }
        }

        internal IEnumerable<IEnvoyTemplateTransform> GetServiceTransforms(List<string> envoyFilenames)
        {
            switch (targetLanguage)
            {
                case TargetLanguage.CSharp:
                    break;
                case TargetLanguage.Rust:
                    yield return new RustService(genNamespace, envoyFilenames, generateClient, generateServer, srcSubdir);
                    break;
                default:
                    throw new NotSupportedException($"Target language {targetLanguage} is not supported.");
            }
        }

        internal IEnumerable<IEnvoyTemplateTransform> GetProjectTransforms(List<SerializationFormat> genFormats, string? sdkPath, bool generateProject)
        {
            switch (targetLanguage)
            {
                case TargetLanguage.CSharp:
                    yield return new DotNetProject(projectName, sdkPath);
                    break;
                case TargetLanguage.Rust:
                    yield return new RustLib(genNamespace, generateProject, srcSubdir);
                    yield return new RustCargoToml(projectName, genFormats, sdkPath, generateProject, srcSubdir);
                    break;
                default:
                    throw new NotSupportedException($"Target language {targetLanguage} is not supported.");
            }
        }

        internal IEnumerable<IEnvoyTemplateTransform> GetResourceTransforms(List<SerializationFormat> genFormats)
        {
            foreach (SerializationFormat genFormat in genFormats)
            {
                string serializerSubNamespace = formatSerializers[genFormat].SubNamespace;

                var (folder, ext) = targetLanguage switch
                {
                    TargetLanguage.CSharp => ("csharp", "cs"),
                    TargetLanguage.Rust => ("rust", "rs"),
                    _ => throw GetLanguageNotRecognizedException(targetLanguage)
                };

                foreach (string subNamespace in new List<string> { ResourceTransform.LanguageCommonFolder, serializerSubNamespace })
                {
                    foreach (string resourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                    {
                        Regex rx = new($"^{Assembly.GetExecutingAssembly().GetName().Name}\\.{ResourceTransform.LanguageResourcesFolder}\\.{folder}\\.({subNamespace})(?:\\.(\\w+(?:\\.\\w+)*))?\\.(\\w+)\\.{ext}$", RegexOptions.IgnoreCase);
                        Match? match = rx.Match(resourceName);
                        if (match.Success)
                        {
                            string subFolder = match.Groups[1].Captures[0].Value;
                            string resourcePath = match.Groups[2].Captures.Count > 0 ? match.Groups[2].Captures[0].Value : string.Empty;
                            string resourceFile = match.Groups[3].Captures[0].Value;

                            StreamReader resourceReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!);

                            yield return new ResourceTransform(targetLanguage, projectName, subFolder, resourcePath, resourceFile, ext, resourceReader.ReadToEnd(), srcSubdir);
                        }
                    }
                }
            }
        }

        private static bool DoesTopicReferToExecutor(string? topic)
        {
            return topic != null && topic.Contains(MqttTopicTokens.CommandExecutorId);
        }

        private static bool DoesTopicReferToMaintainer(string? topic)
        {
            return topic != null && topic.Contains(MqttTopicTokens.PropertyMaintainerId);
        }

        private static Exception GetLanguageNotRecognizedException(TargetLanguage language)
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
