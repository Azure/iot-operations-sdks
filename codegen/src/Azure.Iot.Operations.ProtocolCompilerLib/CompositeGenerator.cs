namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using DTDLParser;
    using DTDLParser.Models;

    public static class CompositeGenerator
    {
        public static async Task<Dictionary<string, string>> GetCodeFilesForModelId(string modelId, string projectName, string codingLanguage, DtmiResolverAsync dtmiResolverAsync, string? sdkPath, bool generateClient, bool generateServer, bool defaultImpl, bool generateProject)
        {
            if (!Dtmi.TryCreateDtmi(modelId, out Dtmi? modelDtmi))
            {
                throw new ArgumentException($"modelId \"{modelId}\" is not a validly formatted model identifier");
            }

            ModelSelector.ContextualizedInterface contextualizedInterface = await ModelSelector.GetInterfaceAndModelContext(Array.Empty<string>(), Array.Empty<string>(), modelDtmi, dtmiResolverAsync, null);

            CodeName genNamespace = new CodeName(contextualizedInterface.InterfaceId!);

            DTInterfaceInfo dtInterface = (DTInterfaceInfo)contextualizedInterface.ModelDict![contextualizedInterface.InterfaceId!];
            var schemaGenerator = new SchemaGenerator(contextualizedInterface.ModelDict!, projectName, dtInterface, contextualizedInterface.MqttVersion, genNamespace);

            string annexText = string.Empty;
            schemaGenerator.GenerateInterfaceAnnex((schemaText, fileName, _) => { annexText = schemaText; }, null);

            Dictionary<string, string> schemaDict = new();
            schemaGenerator.GenerateTelemetrySchemas((schemaText, fileName, _) => { schemaDict[fileName] = schemaText; }, null);
            schemaGenerator.GenerateCommandSchemas((schemaText, fileName, _) => { schemaDict[fileName] = schemaText; }, null);
            schemaGenerator.GenerateObjects((schemaText, fileName, _) => { schemaDict[fileName] = schemaText; }, null);
            schemaGenerator.GenerateEnums((schemaText, fileName, _) => { schemaDict[fileName] = schemaText; }, null);
            schemaGenerator.GenerateJsonErrorFields((schemaText, fileName, _) => { schemaDict[fileName] = schemaText; }, null);
            schemaGenerator.GenerateArrays((schemaText, fileName, _) => { schemaDict[fileName] = schemaText; });
            schemaGenerator.GenerateMaps((schemaText, fileName, _) => { schemaDict[fileName] = schemaText; });
            schemaGenerator.CopyIncludedSchemas((schemaText, fileName, _) => { schemaDict[fileName] = schemaText; });

            TargetLanguage targetLanguage = codingLanguage switch
            {
                "csharp" => TargetLanguage.CSharp,
                "go" => TargetLanguage.Go,
                "rust" => TargetLanguage.Rust,
                _ => throw new ArgumentException($"Unsupported coding language: {codingLanguage}")
            };

            ISchemaStandardizer schemaStandardizer = schemaGenerator.SerializationFormat switch
            {
                PayloadFormat.Avro => new AvroSchemaStandardizer(),
                PayloadFormat.Json => new JsonSchemaStandardizer(),
                _ => throw new NotSupportedException($"Unsupported payload serialization format: {schemaGenerator.SerializationFormat}")
            };

            ITypeGenerator typeGenerator = targetLanguage switch
            {
                TargetLanguage.CSharp => new DotNetTypeGenerator(),
                TargetLanguage.Go => new GoTypeGenerator(),
                TargetLanguage.Rust => new RustTypeGenerator(),
                _ => null!,
            };

            var recursionChecker = new RecursionChecker();
            Dictionary<string, string> codeDict = new();

            foreach (KeyValuePair<string, string> schema in schemaDict)
            {
                foreach (SchemaType schemaType in schemaStandardizer.GetStandardizedSchemas(schema.Value, genNamespace, refString => schemaDict[refString]))
                {
                    if (recursionChecker.TryDetectLoop(schemaType, out CodeName? selfReferencedName))
                    {
                        throw new RecursionException(selfReferencedName);
                    }

                    typeGenerator.GenerateTypeFromSchema((typeText, fileName, _) => { codeDict[fileName] = typeText; }, projectName, schemaType, schemaStandardizer.SerializationFormat);
                }
            }

            using (JsonDocument annexDoc = JsonDocument.Parse(annexText))
            {
                foreach (ITemplateTransform templateTransform in EnvoyTransformFactory.GetTransforms(codingLanguage, projectName, annexDoc, null, sdkPath, generateClient, generateServer, defaultImpl, string.Empty, null, generateProject))
                {
                    codeDict[templateTransform.FileName] = templateTransform.TransformText();
                }
            }

            return codeDict;
        }
    }
}
