namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;

    internal class EnvoyTransformFactory
    {
        private readonly TargetLanguage targetLanguage;
        private readonly CodeName genNamespace;
        private readonly string projectName;
        private readonly string srcSubdir;
        private readonly bool generateClient;
        private readonly bool generateServer;
        private readonly bool defaultImpl;

        internal EnvoyTransformFactory(
            TargetLanguage targetLanguage,
            CodeName genNamespace,
            string projectName,
            string srcSubdir,
            bool generateClient,
            bool generateServer,
            bool defaultImpl)
        {
            this.targetLanguage = targetLanguage;
            this.genNamespace = genNamespace;
            this.projectName = projectName;
            this.srcSubdir = srcSubdir;
            this.generateClient = generateClient;
            this.generateServer = generateServer;
            this.defaultImpl = defaultImpl;
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
            CodeName serviceName,
            string actionName,
            string? inputSchemaType,
            string? outputSchemaType,
            SerializationFormat format,
            string? serviceGroupId,
            string topicPattern,
            bool idempotent,
            List<string> normalResultFields,
            List<ValueTracker<StringHolder>> normalRequiredFields,
            string? normalResultSchema,
            string? errorResultName,
            string? errorResultSchema,
            string? headerCodeName,
            string? headerCodeSchema,
            string? headerInfoName,
            string? headerInfoSchema,
            List<string>? codeValues,
            bool doesCommandTargetExecutor)
        {
            string serializerClassName = format.GetSerializerClassName();
            EmptyTypeName serializerEmptyType = format.GetEmptyTypeName();

            ITypeName? inputSchema = inputSchemaType != null ? new CodeName(inputSchemaType) : null;
            ITypeName? outputSchema = outputSchemaType != null ? new CodeName(outputSchemaType) : null;

            List<CodeName> normalFields = normalResultFields.Select(f => new CodeName(f)).ToList();
            List<CodeName> requiredFields = normalRequiredFields.Select(f => new CodeName(f.Value.Value)).ToList();
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

        internal IEnumerable<IEnvoyTemplateTransform> GetPropertyTransforms(
            SchemaNamer schemaNamer,
            string modelId,
            CodeName serviceName,
            string propertyName,
            string propSchema,
            string? readRespSchema,
            string? writeReqSchema,
            string? writeRespSchema,
            string? propValueName,
            string? readErrorName,
            string? readErrorSchema,
            string? writeErrorName,
            string? writeErrorSchema,
            SerializationFormat readFormat,
            SerializationFormat writeFormat,
            string readTopicPattern,
            string writeTopicPattern,
            bool separateProperties,
            bool doesPropertyTargetReadMaintainer,
            bool doesPropertyTargetWriteMaintainer)
        {
            string readSerializerClassName = readFormat.GetSerializerClassName();
            EmptyTypeName readSerializerEmptyType = readFormat.GetEmptyTypeName();
            string writeSerializerClassName = writeFormat.GetSerializerClassName();
            EmptyTypeName writeSerializerEmptyType = writeFormat.GetEmptyTypeName();

            switch (targetLanguage)
            {
                case TargetLanguage.CSharp:
                    if (generateServer)
                    {
                        yield return new DotNetPropertyMaintainer(
                            propertyName,
                            schemaNamer.GetPropMaintainerBinder(propertyName),
                            schemaNamer.ReadResponderBinder,
                            schemaNamer.WriteResponderBinder,
                            schemaNamer.GetPropReadActName(propertyName),
                            schemaNamer.GetPropWriteActName(propertyName),
                            projectName,
                            genNamespace,
                            modelId,
                            serviceName,
                            readSerializerClassName,
                            readSerializerEmptyType,
                            writeSerializerClassName,
                            writeSerializerEmptyType,
                            readRespSchema,
                            writeReqSchema,
                            writeRespSchema,
                            readTopicPattern,
                            writeTopicPattern);
                    }

                    if (generateClient)
                    {
                        yield return new DotNetPropertyConsumer(
                            propertyName,
                            schemaNamer.GetPropConsumerBinder(propertyName),
                            schemaNamer.ReadRequesterBinder,
                            schemaNamer.WriteRequesterBinder,
                            schemaNamer.GetPropReadActName(propertyName),
                            schemaNamer.GetPropWriteActName(propertyName),
                            projectName,
                            genNamespace,
                            modelId,
                            serviceName,
                            readSerializerClassName,
                            readSerializerEmptyType,
                            writeSerializerClassName,
                            writeSerializerEmptyType,
                            readRespSchema,
                            writeReqSchema,
                            writeRespSchema,
                            readTopicPattern,
                            writeTopicPattern);
                    }

                    break;
                case TargetLanguage.Rust:
                    if (generateServer)
                    {
                        yield return new RustPropertyMaintainer(
                            propertyName,
                            new CodeName(propSchema),
                            schemaNamer.GetPropMaintainerBinder(propertyName),
                            schemaNamer.GetPropReadActName(propertyName),
                            schemaNamer.GetPropWriteActName(propertyName),
                            genNamespace,
                            modelId,
                            readSerializerEmptyType,
                            writeSerializerEmptyType,
                            readRespSchema,
                            writeReqSchema,
                            writeRespSchema,
                            propValueName,
                            readErrorName,
                            readErrorSchema,
                            writeErrorName,
                            writeErrorSchema,
                            readTopicPattern,
                            writeTopicPattern,
                            srcSubdir,
                            separateProperties);
                    }

                    if (generateClient)
                    {
                        yield return new RustPropertyConsumer(
                            propertyName,
                            new CodeName(propSchema),
                            schemaNamer.GetPropConsumerBinder(propertyName),
                            schemaNamer.GetPropReadActName(propertyName),
                            schemaNamer.GetPropWriteActName(propertyName),
                            genNamespace,
                            modelId,
                            readSerializerEmptyType,
                            writeSerializerEmptyType,
                            readRespSchema,
                            writeReqSchema,
                            writeRespSchema,
                            propValueName,
                            readErrorName,
                            readErrorSchema,
                            writeErrorName,
                            writeErrorSchema,
                            readTopicPattern,
                            writeTopicPattern,
                            srcSubdir,
                            doesPropertyTargetReadMaintainer,
                            doesPropertyTargetWriteMaintainer,
                            separateProperties);
                    }

                    break;
                default:
                    throw new NotSupportedException($"Target language {targetLanguage} is not supported.");
            }
        }

        internal IEnumerable<IEnvoyTemplateTransform> GetEventTransforms(
            SchemaNamer schemaNamer,
            string modelId,
            CodeName serviceName,
            string eventName,
            string schemaType,
            SerializationFormat format,
            string? serviceGroupId,
            string topicPattern)
        {
            string serializerClassName = format.GetSerializerClassName();
            EmptyTypeName serializerEmptyType = format.GetEmptyTypeName();

            switch (targetLanguage)
            {
                case TargetLanguage.CSharp:
                    if (generateServer)
                    {
                        yield return new DotNetTelemetrySender(eventName, schemaNamer.GetEventSenderBinder(eventName), projectName, genNamespace, modelId, serviceName, serializerClassName, serializerEmptyType, new CodeName(schemaType), topicPattern);
                    }

                    if (generateClient)
                    {
                        yield return new DotNetTelemetryReceiver(eventName, schemaNamer.GetEventReceiverBinder(eventName), projectName, genNamespace, modelId, serviceName, serializerClassName, serializerEmptyType, new CodeName(schemaType), serviceGroupId, topicPattern);
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

        internal IEnumerable<IEnvoyTemplateTransform> GetAggregateErrorTransforms(AggregateErrorSpec errorSpec)
        {
            switch (targetLanguage)
            {
                case TargetLanguage.CSharp:
                    yield return new DotNetAggregateError(
                        projectName,
                        new CodeName(errorSpec.SchemaName),
                        genNamespace,
                        errorSpec.InnerErrors.Select(kv => (new CodeName(kv.Key), new CodeName(kv.Value))).ToList());
                    break;
                case TargetLanguage.Rust:
                    yield return new RustAggregateError(
                        new CodeName(errorSpec.SchemaName),
                        genNamespace,
                        errorSpec.InnerErrors.Select(kv => (new CodeName(kv.Key), new CodeName(kv.Value))).ToList(),
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

        internal IEnumerable<IEnvoyTemplateTransform> GetServiceTransforms(SchemaNamer schemaNamer, CodeName serviceName, List<ActionSpec> actionSpecs, List<PropertySpec> propSpecs, List<EventSpec> eventSpecs)
        {
            switch (targetLanguage)
            {
                case TargetLanguage.CSharp:

                    yield return new DotNetService(
                            schemaNamer.ReadRequesterBinder,
                            schemaNamer.WriteRequesterBinder,
                            schemaNamer.ReadResponderBinder,
                            schemaNamer.WriteResponderBinder,
                            projectName,
                            genNamespace,
                            serviceName,
                            actionSpecs,
                            propSpecs,
                            eventSpecs,
                            generateClient,
                            generateServer,
                            defaultImpl);

                    break;
                case TargetLanguage.Rust:
                    yield break;
                default:
                    throw new NotSupportedException($"Target language {targetLanguage} is not supported.");
            }
        }

        internal IEnumerable<IEnvoyTemplateTransform> GetProjectTransforms(List<SerializationFormat> genFormats, string? sdkPath, List<string> envoyFilenames, bool generateProject)
        {
            switch (targetLanguage)
            {
                case TargetLanguage.CSharp:
                    yield return new DotNetProject(projectName, sdkPath);
                    break;
                case TargetLanguage.Rust:
                    yield return new RustIndex(genNamespace, envoyFilenames, generateClient, generateServer, srcSubdir);
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
                string serializerSubNamespace = genFormat.GetSerializerSubNamespace();

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

        private static Exception GetLanguageNotRecognizedException(TargetLanguage language)
        {
            return new Exception($"language '{language}' not recognized");
        }
    }
}
