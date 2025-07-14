namespace Yaml2Dtdl
{
    using System;
    using System.Linq;
    using System.IO;
    using System.Collections.Generic;
    using DTDLParser;
    using OpcUaDigest;
    using SpecMapper;
    using YamlDotNet.Serialization;

    internal class Program
    {
        private const int defaultMaxErrors = 4;

        private const string coreSpecName = "OpcUaCore";
        private const string sourceFileSuffix = ".digest.yaml";
        private const string destFileSuffix = ".dtdl.json";

        private static readonly IDeserializer deserializer;

        private static readonly Uri badDtmiOrTermValidationId = new Uri("dtmi:dtdl:parsingError:badDtmiOrTerm");
        private static readonly Uri idRefBadDtmiOrTermValidationId = new Uri("dtmi:dtdl:parsingError:idRefBadDtmiOrTerm");

        private static string sourceRoot = string.Empty;
        private static string destRoot = string.Empty;
        private static string unitTypesFile = string.Empty;
        private static string cotypeRulesFile = string.Empty;
        private static string resolverConfig = string.Empty;

        static Program()
        {
            deserializer = new DeserializerBuilder()
                .WithTypeDiscriminatingNodeDeserializer(options =>
                {
                    options.AddUniqueKeyTypeDiscriminator<OpcUaDataType>(
                        ("Enums", typeof(OpcUaEnum)),
                        ("Fields", typeof(OpcUaObj)),
                        ("Bases", typeof(OpcUaSub)));
                })
                .WithTypeConverter(new OpcUaDefinedTypeConverter())
                .WithTypeConverter(new StringIntTupleTypeConverter())
                .Build();
        }

        static void Main(string[] args)
        {
            if (args.Length < 5)
            {
                Console.WriteLine("usage: Yaml2Dtdl <SOURCE_ROOT> <DEST_ROOT> <UNIT_TYPES> <COTYPE_RULES> <RESOLVER> [ <INDEX> [ <MAX_ERRORS> ] ]");
                return;
            }

            sourceRoot = args[0];
            destRoot = args[1];
            unitTypesFile = args[2];
            cotypeRulesFile = args[3];
            resolverConfig = args[4];

            string? indexFilePath = args.Length > 5 ? args[5] : null;
            int maxErrors = args.Length > 6 ? int.Parse(args[6]) : defaultMaxErrors;

            Dictionary<int, (string, string)> unitTypesDict = File.ReadAllLines(unitTypesFile).Select(l => l.Split(',')).ToDictionary(v => int.Parse(v[0]), v => (v[1], v[2]));

            Resolver resolver = new Resolver(resolverConfig);
            ParsingOptions parsingOptions = new ParsingOptions() { DtmiResolver = resolver.Resolve, AllowUndefinedExtensions = WhenToAllow.Never };
            parsingOptions.ExtensionLimitContexts.Add(new Dtmi("dtmi:dtdl:limits:aio"));
            parsingOptions.ExtensionLimitContexts.Add(new Dtmi("dtmi:dtdl:limits:onvif"));
            ModelParser modelParser = new ModelParser(parsingOptions);

            List<string> invalidModels = new List<string>();
            HashSet<string> unrecognizedTypes = new HashSet<string>();
            HashSet<string> undefinedIdentifiers = new HashSet<string>();

            string coreYamlFileName = $"{coreSpecName}{sourceFileSuffix}";
            string coreYamlFilePath = Path.Combine(sourceRoot, coreYamlFileName);
            string coreYamlFileText = File.ReadAllText(coreYamlFilePath);
            OpcUaDigest coreOpcUaDigest = deserializer.Deserialize<OpcUaDigest>(coreYamlFileText);

            if (!Directory.Exists(destRoot))
            {
                Directory.CreateDirectory(destRoot);
            }

            Dictionary<string, List<string>> objectTypeIdSupers = new();
            HashSet<string> typeDefinitions = new();
            SpecMapper specMapper = new ();
            foreach (string yamlFilePath in Directory.GetFiles(sourceRoot, $"*{sourceFileSuffix}"))
            {
                Preload(yamlFilePath, objectTypeIdSupers, typeDefinitions, specMapper);
            }

            CotypeRuleEngine cotypeRuleEngine = new CotypeRuleEngine(objectTypeIdSupers, cotypeRulesFile);

            List<SpecInfo> specInfos = new ();

            foreach (string yamlFilePath in Directory.GetFiles(sourceRoot, $"*{sourceFileSuffix}"))
            {
                string yamlFileName = Path.GetFileName(yamlFilePath);
                string specName = yamlFileName.Substring(0, yamlFileName.Length - sourceFileSuffix.Length);

                ConvertToDtdl(specMapper, coreOpcUaDigest, yamlFilePath, destRoot, specName, unitTypesDict, objectTypeIdSupers, typeDefinitions, cotypeRuleEngine, specInfos);
            }

            Console.WriteLine();
            cotypeRuleEngine.DisplayStats();

            if (maxErrors == 0)
            {
                return;
            }

            if (indexFilePath != null)
            {
                Console.WriteLine();
                Console.WriteLine($"Writing model index to file {indexFilePath}");

                using (StreamWriter indexFile = new StreamWriter(indexFilePath))
                {
                    indexFile.WriteLine("[");

                    int ix = 1;
                    foreach (SpecInfo specInfo in specInfos)
                    {
                        specInfo.WriteToStream(indexFile, addComma: ix < specInfos.Count);
                        ix++;
                    }

                    indexFile.WriteLine("]");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Parsing models to validate correctness....");

            foreach (string modelFilePath in Directory.GetFiles(destRoot, $"*{destFileSuffix}"))
            {
                CheckDtdl(modelFilePath, invalidModels, unrecognizedTypes, undefinedIdentifiers, modelParser, maxErrors);
            }

            if (invalidModels.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"Invalid DTDL generated in the following files:");
                foreach (string invalidModel in invalidModels)
                {
                    Console.WriteLine($"  {invalidModel}");
                }
            }
            else
            {
                Console.WriteLine("All generated files contain valid DTDL.");
            }

            if (unrecognizedTypes.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"Unrecognized types:");
                foreach (string unrecognizedType in unrecognizedTypes.Order())
                {
                    Console.WriteLine($"  {unrecognizedType}");
                }
            }
            else
            {
                Console.WriteLine("No unrecognized types.");
            }

            if (undefinedIdentifiers.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"Undefined identifiers:");
                foreach (string undefinedIdentifier in undefinedIdentifiers.Order())
                {
                    Console.WriteLine($"  {undefinedIdentifier}");
                }
            }
            else
            {
                Console.WriteLine("No undefined identifiers.");
            }
        }

        private static void Preload(string sourceFilePath, Dictionary<string, List<string>> objectTypeIdSupers, HashSet<string> typeDefinitions, SpecMapper specMapper)
        {
            string yamlFileName = Path.GetFileName(sourceFilePath);

            Console.WriteLine($"Preloading file {yamlFileName}");

            string sourceFileText = File.ReadAllText(sourceFilePath);

            OpcUaDigest opcUaDigest = deserializer.Deserialize<OpcUaDigest>(sourceFileText);
            specMapper.PreloadUri(opcUaDigest.SpecUri);

            if (opcUaDigest.DefinedTypes != null)
            {
                foreach (KeyValuePair<string, OpcUaDefinedType> definedType in opcUaDigest.DefinedTypes)
                {
                    List<string> superTypeIds = definedType.Value.Contents.Where(c => c.Relationship == "HasSubtype_reverse").Select(c => TypeConverter.GetModelId(c.DefinedType)).ToList();
                    string objectTypeId = TypeConverter.GetModelId(definedType.Value);

                    objectTypeIdSupers[objectTypeId] = superTypeIds;

                    foreach (OpcUaContent content in definedType.Value.Contents)
                    {
                        if (content.Relationship == "HasComponent" && content.DefinedType.NodeType == "UAObject")
                        {
                            string? typeDefinition = content.DefinedType.Contents.FirstOrDefault(c => c.Relationship == "HasTypeDefinition" && c.DefinedType.NodeType == "UAObjectType")?.DefinedType?.BrowseName;
                            if (typeDefinition != null)
                            {
                                typeDefinitions.Add(typeDefinition);
                            }
                        }
                    }
                }
            }
        }

        private static bool DoesAncestorHaveType(string objectTypeId, string keyTypeId, Dictionary<string, List<string>> objectTypeIdSupers)
        {
            foreach (string supertype in objectTypeIdSupers[objectTypeId])
            {
                if (supertype == keyTypeId || DoesAncestorHaveType(supertype, keyTypeId, objectTypeIdSupers))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ConvertToDtdl(SpecMapper specMapper, OpcUaDigest coreOpcUaDigest, string yamlFilePath, string destRoot, string specName, Dictionary<int, (string, string)> unitTypesDict, Dictionary<string, List<string>> objectTypeIdSupers, HashSet<string> typeDefinitions, CotypeRuleEngine cotypeRuleEngine, List<SpecInfo> specInfos)
        {
            Console.WriteLine($"Processing file {yamlFilePath}");

            string yamlFileText = File.ReadAllText(yamlFilePath);
            OpcUaDigest opcUaDigest = deserializer.Deserialize<OpcUaDigest>(yamlFileText);

            if (opcUaDigest.DefinedTypes != null)
            {
                string outFilePath = Path.Combine(destRoot, $"{specName}{destFileSuffix}");
                Console.WriteLine($"  Writing file {outFilePath}");

                SpecInfo specInfo = new SpecInfo(Path.GetFileName(outFilePath), specMapper.GetUriFromSpecName(specName));

                using (StreamWriter outputFile = new StreamWriter(outFilePath))
                {
                    outputFile.WriteLine("[");

                    int ix = 1;
                    foreach (KeyValuePair<string, OpcUaDefinedType> definedType in opcUaDigest.DefinedTypes)
                    {
                        string modelId = TypeConverter.GetModelId(definedType.Value);
                        bool isEvent = DoesAncestorHaveType(TypeConverter.GetModelId(definedType.Value), "dtmi:opcua:OpcUaCore:BaseEventType", objectTypeIdSupers);
                        bool isComposite = !typeDefinitions.Contains(definedType.Key) && definedType.Value.Datatype != "Abstract" && !isEvent;
                        bool appendComma = ix < opcUaDigest.DefinedTypes.Count;
                        DtdlInterface dtdlInterface = new DtdlInterface(specMapper, modelId, isComposite, isEvent, definedType.Value, opcUaDigest.DataTypes, coreOpcUaDigest.DataTypes, unitTypesDict, cotypeRuleEngine, appendComma);
                        string jsonText = dtdlInterface.TransformText();

                        outputFile.Write(jsonText);

                        specInfo.AddComponent($"{modelId};1", definedType.Value.DisplayName, TypeConverter.GetTypeRefFromNodeId(specMapper, definedType.Value.NodeId), isComposite, isEvent);

                        ix++;
                    }

                    outputFile.WriteLine("]");
                }

                specInfos.Add(specInfo);
            }
        }

        private static void CheckDtdl(string modelFilePath, List<string> invalidModels, HashSet<string> unrecognizedTypes, HashSet<string> undefinedIdentifiers, ModelParser modelParser, int maxErrors)
        {
            DtdlParseLocator parseLocator = (int parseIndex, int parseLine, out string sourceName, out int sourceLine) =>
            {
                sourceName = Path.GetFileName(modelFilePath);
                sourceLine = parseLine;
                return true;
            };

            try
            {
                string jsonText = File.ReadAllText(modelFilePath);
                modelParser.Parse(jsonText, parseLocator);
            }
            catch (ParsingException ex)
            {
                Console.WriteLine($"  Generated model {modelFilePath} has {ex.Errors.Count} validation errors:");

                foreach (ParsingError err in ex.Errors)
                {
                    if (err.ValidationID == badDtmiOrTermValidationId || err.ValidationID == idRefBadDtmiOrTermValidationId)
                    {
                        unrecognizedTypes.Add(err.Value);
                    }
                }

                int ix = 0;
                foreach (ParsingError err in ex.Errors)
                {
                    ix++;
                    if (ix > maxErrors)
                    {
                        Console.WriteLine($"    ...and {ex.Errors.Count - maxErrors} more violations");
                        break;
                    }

                    Console.WriteLine($"    {err.Message}");
                }

                invalidModels.Add(modelFilePath);
            }
            catch (ResolutionException ex)
            {
                Console.WriteLine($"  Generated model {modelFilePath} has {ex.UndefinedIdentifiers.Count} undefined identifiers:");

                foreach (Dtmi dtmi in ex.UndefinedIdentifiers)
                {
                    undefinedIdentifiers.Add(dtmi.AbsoluteUri);
                    Console.WriteLine($"    {dtmi}");
                }
            }
        }
    }
}
