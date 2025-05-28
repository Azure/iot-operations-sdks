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
        private static readonly ModelParser modelParser;

        private static readonly Uri badDtmiOrTermValidationId = new Uri("dtmi:dtdl:parsingError:badDtmiOrTerm");
        private static readonly Uri idRefBadDtmiOrTermValidationId = new Uri("dtmi:dtdl:parsingError:idRefBadDtmiOrTerm");

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

            ParsingOptions parsingOptions = new ParsingOptions() { AllowUndefinedExtensions = WhenToAllow.Always };
            parsingOptions.ExtensionLimitContexts.Add(new Dtmi("dtmi:dtdl:limits:onvif"));
            modelParser = new ModelParser(parsingOptions);
        }

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("usage: Yaml2Dtdl <SOURCE_ROOT> <DEST_ROOT> [ <MAX_ERRORS> ]");
                return;
            }

            string sourceRoot = args[0];
            string destRoot = args[1];

            int maxErrors = args.Length > 2 ? int.Parse(args[2]) : defaultMaxErrors;

            List<string> invalidModels = new List<string>();
            HashSet<string> unrecognizedTypes = new HashSet<string>();

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

                ConvertToDtdl(coreOpcUaDigest, deserializer, yamlFilePath, destRoot, specName, invalidModels, unrecognizedTypes, maxErrors);
            }

            if (invalidModels.Count > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Invalid DTDL generated in the following files:");
                foreach (string invalidModel in invalidModels)
                {
                    Console.WriteLine($"  {invalidModel}");
                }
                Console.ResetColor();
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
        }

        public static void ConvertToDtdl(OpcUaDigest coreOpcUaDigest, IDeserializer deserializer, string yamlFilePath, string destRoot, string specName, List<string> invalidModels, HashSet<string> unrecognizedTypes, int maxErrors)
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
                    string modelId = TypeConverter.GetModelId(specName, definedType.Key);
                    string outFileName = $"{definedType.Key}{destFileSuffix}";
                    string outFilePath = Path.Combine(outFolderPath, outFileName);

                    DtdlInterface dtdlInterface = new DtdlInterface(modelId, definedType.Value, opcUaDigest.DataTypes, coreOpcUaDigest.DataTypes);
                    string jsonText = dtdlInterface.TransformText();

                    Console.WriteLine($"  Writing file {outFilePath}");

                    using (StreamWriter outputFile = new StreamWriter(outFilePath))
                    {
                        outputFile.Write(jsonText);
                    }

                    DtdlParseLocator parseLocator = (int parseIndex, int parseLine, out string sourceName, out int sourceLine) =>
                    {
                        sourceName = outFileName;
                        sourceLine = parseLine;
                        return true;
                    };

                    if (maxErrors > 0)
                    {
                        try
                        {
                            modelParser.Parse(jsonText, parseLocator);
                        }
                        catch (ParsingException ex)
                        {
                            string relativeFilePath = Path.Combine(specName, outFileName);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"  Generated model {relativeFilePath} has {ex.Errors.Count} validation errors:");

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

                            Console.ResetColor();

                            invalidModels.Add(outFilePath);
                        }
                    }
                }
            }
        }
    }
}
