namespace Yaml2Dtdl
{
    using System;
    using System.Linq;
    using System.IO;
    using System.Collections.Generic;
    using DTDLParser;
    using OpcUaDigest;
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
            if (args.Length < 4)
            {
                Console.WriteLine("usage: Yaml2Dtdl <SOURCE_ROOT> <DEST_ROOT> <UNIT_TYPES> <RESOLVER> [ <MAX_ERRORS> ]");
                return;
            }

            sourceRoot = args[0];
            destRoot = args[1];
            unitTypesFile = args[2];
            resolverConfig = args[3];

            int maxErrors = args.Length > 4 ? int.Parse(args[4]) : defaultMaxErrors;

            Dictionary<int, (string, string)> unitTypesDict = File.ReadAllLines(unitTypesFile).Select(l => l.Split(',')).ToDictionary(v => int.Parse(v[0]), v => (v[1], v[2]));

            Resolver resolver = new Resolver(resolverConfig);
            ParsingOptions parsingOptions = new ParsingOptions() { DtmiResolver = resolver.Resolve, AllowUndefinedExtensions = WhenToAllow.Always };
            parsingOptions.ExtensionLimitContexts.Add(new Dtmi("dtmi:dtdl:limits:onvif"));
            ModelParser modelParser = new ModelParser(parsingOptions);

            List<string> invalidModels = new List<string>();
            HashSet<string> unrecognizedTypes = new HashSet<string>();
            HashSet<string> undefinedIdentifiers = new HashSet<string>();

            string coreYamlFileName = $"{coreSpecName}{sourceFileSuffix}";
            string coreYamlFilePath = Path.Combine(sourceRoot, coreYamlFileName);
            string coreYamlFileText = File.ReadAllText(coreYamlFilePath);
            OpcUaDigest coreOpcUaDigest = deserializer.Deserialize<OpcUaDigest>(coreYamlFileText);

            foreach (string yamlFilePath in Directory.GetFiles(sourceRoot, $"*{sourceFileSuffix}"))
            {
                if (Path.GetFileName(yamlFilePath) == coreYamlFileName)
                {
                    continue;
                }

                string yamlFileName = Path.GetFileName(yamlFilePath);
                string specName = yamlFileName.Substring(0, yamlFileName.Length - sourceFileSuffix.Length);

                ConvertToDtdl(coreOpcUaDigest, deserializer, yamlFilePath, destRoot, specName, unitTypesDict);
            }

            if (maxErrors == 0)
            {
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Parsing models to validate correctness....");

            foreach (string modelFolderPath in Directory.GetDirectories(destRoot))
            {
                foreach (string modelFilePath in Directory.GetFiles(modelFolderPath, $"*{destFileSuffix}"))
                {
                    CheckDtdl(modelFilePath, invalidModels, unrecognizedTypes, undefinedIdentifiers, modelParser, maxErrors);
                }
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

        private static void ConvertToDtdl(OpcUaDigest coreOpcUaDigest, IDeserializer deserializer, string yamlFilePath, string destRoot, string specName, Dictionary<int, (string, string)> unitTypesDict)
        {
            Console.WriteLine($"Processing file {yamlFilePath}");

            string yamlFileText = File.ReadAllText(yamlFilePath);
            OpcUaDigest opcUaDigest = deserializer.Deserialize<OpcUaDigest>(yamlFileText);

            string outFolderPath = Path.Combine(destRoot, specName);
            if (!Directory.Exists(outFolderPath))
            {
                Directory.CreateDirectory(outFolderPath);
            }

            if (opcUaDigest.DefinedTypes != null)
            {
                foreach (KeyValuePair<string, OpcUaDefinedType> definedType in opcUaDigest.DefinedTypes)
                {
                    string modelId = TypeConverter.GetModelId(definedType.Value);
                    string outFileName = $"{TypeConverter.Dequalify(definedType.Key)}{destFileSuffix}";
                    string outFilePath = Path.Combine(outFolderPath, outFileName);

                    DtdlInterface dtdlInterface = new DtdlInterface(modelId, definedType.Value, opcUaDigest.DataTypes, coreOpcUaDigest.DataTypes, unitTypesDict);
                    string jsonText = dtdlInterface.TransformText();

                    Console.WriteLine($"  Writing file {outFilePath}");

                    using (StreamWriter outputFile = new StreamWriter(outFilePath))
                    {
                        outputFile.Write(jsonText);
                    }
                }
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
