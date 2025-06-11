namespace FilterYaml
{
    using System;
    using System.Linq;
    using System.IO;
    using System.Collections.Generic;
    using OpcUaDigest;
    using YamlDotNet.Serialization;

    internal class Program
    {
        private const string coreSpecName = "OpcUaCore";
        private const string fileSuffix = ".digest.yaml";

        private static readonly IDeserializer deserializer;

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
            if (args.Length < 2)
            {
                Console.WriteLine("usage: FilterYaml <SOURCE_ROOT> <DEST_ROOT> [ <FILE_NAME> ]");
                return;
            }

            string sourceRoot = args[0];
            string destRoot = args[1];
            string? singleFileName = args.Length > 2 ? args[2] : null;

            if (!Directory.Exists(destRoot))
            {
                Directory.CreateDirectory(destRoot);
            }

            string coreYamlFileName = $"{coreSpecName}{fileSuffix}";

            foreach (string sourceFilePath in Directory.GetFiles(sourceRoot, $"*{fileSuffix}"))
            {
                if (Path.GetFileName(sourceFilePath) == coreYamlFileName)
                {
                    string outFilePath = Path.Combine(destRoot, Path.GetFileName(sourceFilePath));
                    File.Copy(sourceFilePath, outFilePath, overwrite:true);
                }
                else if (singleFileName == null || Path.GetFileName(singleFileName) == Path.GetFileName(sourceFilePath))
                {
                    Filter(deserializer, sourceFilePath, destRoot);
                }
            }
        }

        private static IEnumerable<OpcUaContent> FilteredContents(OpcUaDefinedType definedType)
        {
            foreach (OpcUaContent content in definedType.Contents)
            {
                if (definedType.NodeType == "UAObjectType" && content.Relationship == "HasComponent" ||
                    definedType.NodeType == "UAObjectType" && content.Relationship == "HasSubtype_reverse" ||
                    definedType.NodeType == "UAVariable" && content.Relationship == "HasComponent" ||
                    definedType.NodeType == "UAVariable" && content.Relationship == "HasProperty" && content.DefinedType.UnitId != null ||
                    definedType.NodeType == "UAMethod" && content.Relationship == "HasProperty" ||
                    definedType.NodeType == "UAObject" && content.Relationship == "HasTypeDefinition" ||
                    content.Relationship == "HasModellingRule")
                {
                    yield return content;
                }
            }
        }

        public static void Filter(IDeserializer deserializer, string sourceFilePath, string destRoot)
        {
            string yamlFileName = Path.GetFileName(sourceFilePath);

            Console.WriteLine($"Filtering file {yamlFileName}");

            string outFilePath = Path.Combine(destRoot, yamlFileName);
            using (StreamWriter outputFile = new StreamWriter(outFilePath))
            {
                string sourceFileText = File.ReadAllText(sourceFilePath);

                using (StringReader stringReader = new StringReader(sourceFileText))
                {
                    string? line;
                    do
                    {
                        line = stringReader.ReadLine();
                        outputFile.WriteLine(line);
                    } while (line != string.Empty);
                }

                OpcUaDigest opcUaDigest = deserializer.Deserialize<OpcUaDigest>(sourceFileText);

                if (opcUaDigest.DefinedTypes != null)
                {
                    outputFile.WriteLine("DefinedTypes:");

                    foreach (KeyValuePair<string, OpcUaDefinedType> definedType in opcUaDigest.DefinedTypes)
                    {
                        if (FilteredContents(definedType.Value).Any())
                        {
                            outputFile.WriteLine();
                            outputFile.WriteLine($"  {definedType.Key}:");
                            Visit(outputFile, 1, definedType.Value);
                        }
                    }
                }
            }
        }

        private static void Visit(StreamWriter outputFile, int depth, OpcUaDefinedType definedType)
        {
            string currentIndent = new string(' ', depth * 2);

            string dataTypeStr = string.Empty;
            string valueRankStr = string.Empty;
            string accessLevelStr = string.Empty;

            if (definedType.Datatype != null)
            {
                dataTypeStr = $", {definedType.Datatype}";
                valueRankStr = $", {definedType.ValueRank}";
                accessLevelStr = $", {definedType.AccessLevel}";
            }

            outputFile.WriteLine($"{currentIndent}- [ {definedType.NodeType}, {definedType.NodeId}, {definedType.BrowseName}{dataTypeStr}{valueRankStr}{accessLevelStr} ]");

            if (definedType.Arguments.Any(a => a.Key != string.Empty))
            {
                outputFile.WriteLine($"{currentIndent}- Arguments:");
                foreach (KeyValuePair<string, (string?, int)> argument in definedType.Arguments)
                {
                    if (argument.Key != string.Empty)
                    {
                        outputFile.WriteLine($"{currentIndent}    {argument.Key}: [ {argument.Value.Item1}, {argument.Value.Item2} ]");
                    }
                }
            }

            if (definedType.UnitId != null)
            {
                outputFile.WriteLine($"{currentIndent}- UnitId: {definedType.UnitId}");
            }

            foreach (OpcUaContent content in FilteredContents(definedType))
            {
                outputFile.WriteLine($"{currentIndent}- {content.Relationship}:");
                Visit(outputFile, depth + 1, content.DefinedType);
            }
        }
    }
}
