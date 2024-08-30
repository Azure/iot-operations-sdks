namespace Akri.Dtdl.Codegen.IntegrationTests.TestCodeGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Akri.Dtdl.Codegen;
    using Akri.Dtdl.Codegen.IntegrationTests.SchemaExtractor;
    using Akri.Dtdl.Codegen.IntegrationTests.T4;
    using DTDLParser;
    using DTDLParser.Models;

    internal class Program
    {
        static readonly string[] SupportedLanguages = { "csharp", "java", "python" };

        static int Main(string[] args)
        {
            try
            {
                if (args.Length < 5)
                {
                    Console.WriteLine("DtdlMqtt.TestCodeGenerator language annexFile modelFile testCaseFolder outRoot");
                    Console.WriteLine($"       language = {string.Join("|", SupportedLanguages)}");
                    return 1;
                }

                string language = args[0];
                string annexFile = args[1];
                string modelFile = args[2];
                string testCaseFolder = args[3];
                string outRoot = args[4];

                var parsingOptions = new ParsingOptions() { AllowUndefinedExtensions = WhenToAllow.Always };
                var modelParser = new ModelParser(parsingOptions);
                string modelText = File.OpenText(modelFile).ReadToEnd();

                if (!SupportedLanguages.Contains(language))
                {
                    Console.WriteLine($"language must be {string.Join(" or ", SupportedLanguages.Select(l => $"'{l}'"))}");
                    return 1;
                }

                DtdlParseLocator parseLocator = (int parseIndex, int parseLine, out string sourceName, out int sourceLine) =>
                {
                    sourceName = modelFile;
                    sourceLine = parseLine;
                    return true;
                };

                IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict;
                try
                {
                    modelDict = modelParser.Parse(modelText, parseLocator);
                }
                catch (ParsingException pex)
                {
                    foreach (ParsingError perr in pex.Errors)
                    {
                        Console.WriteLine(perr.Message);
                    }

                    return 1;
                }

                List<string> testCaseNames = Directory.GetFiles(testCaseFolder, @"*.json").Select(f => Path.GetFileNameWithoutExtension(f)).ToList();

                using (JsonDocument annexDocument = JsonDocument.Parse(File.OpenText(annexFile).ReadToEnd()))
                {
                    string genNamespace = annexDocument.RootElement.GetProperty(AnnexFileProperties.Namespace).GetString()!;
                    string modelId = annexDocument.RootElement.GetProperty(AnnexFileProperties.ModelId).GetString()!;
                    string serviceName = annexDocument.RootElement.GetProperty(AnnexFileProperties.ServiceName).GetString()!;
                    string genFormat = annexDocument.RootElement.GetProperty(AnnexFileProperties.PayloadFormat).GetString()!;
                    string testName = $"{serviceName}As{NameFormatter.Capitalize(genFormat)}";
                    string? commandTopic = annexDocument.RootElement.TryGetProperty(AnnexFileProperties.CommandRequestTopic, out JsonElement cmdTopcElt) ? cmdTopcElt.GetString() : null;
                    bool doesCommandTargetExecutor = commandTopic != null && commandTopic.Contains(MqttTopicTokens.CommandExecutorId);
                    Dtmi interfaceId = new Dtmi(modelId);
                    DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[interfaceId];

                    List<(string?, SchemaTypeInfo)> telemNameSchemas = annexDocument.RootElement.GetProperty(AnnexFileProperties.TelemetryList).EnumerateArray().Select(t => (t.TryGetProperty(AnnexFileProperties.TelemName, out JsonElement telemNameElt) ? telemNameElt.GetString() : null, SchemaExtractor.GetTelemTypeInfo(dtInterface, t))).ToList();

                    List<(string, SchemaTypeInfo?, SchemaTypeInfo?)> cmdNameReqResps = annexDocument.RootElement.GetProperty(AnnexFileProperties.CommandList).EnumerateArray().Select(c => (c.GetProperty(AnnexFileProperties.CommandName).GetString()!, SchemaExtractor.GetCmdReqTypeInfo(dtInterface, c), SchemaExtractor.GetCmdRespTypeInfo(dtInterface, c))).ToList();

                    List<EnumTypeInfo> enumSchemas = SchemaExtractor.GetEnumTypeInfos(modelDict, interfaceId);

                    foreach (ITemplateTransform templateTransform in TemplateTransformFactory.GetTransforms(language, genFormat, modelId, genNamespace, serviceName, testName, testCaseNames, telemNameSchemas, cmdNameReqResps, doesCommandTargetExecutor, enumSchemas))
                    {
                        string folderPath = Path.Combine(outRoot, templateTransform.FolderPath);
                        if (!Directory.Exists(folderPath))
                        {
                            Directory.CreateDirectory(folderPath);
                        }

                        string filePath = Path.Combine(folderPath, templateTransform.FileName);

                        string generatedCode = templateTransform.TransformText();
                        File.WriteAllText(filePath, generatedCode);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DtdlMqtt.TestCodeGenerator failed with exception: {ex.Message}");
                return 1;
            }

            return 0;
        }
    }
}
