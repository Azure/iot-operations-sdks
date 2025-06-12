namespace DedupYaml
{
    using System;
    using System.Linq;
    using System.IO;
    using System.Collections.Generic;
    using OpcUaDigest;
    using YamlDotNet.Serialization;

    internal class Program
    {
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
                Console.WriteLine("usage: DedupYaml <SOURCE_ROOT> <DEST_ROOT>");
                return;
            }

            string sourceRoot = args[0];
            string destRoot = args[1];

            if (!Directory.Exists(destRoot))
            {
                Directory.CreateDirectory(destRoot);
            }

            Dictionary<string, HashSet<string>> objectTypeComponentsAndProperties = new ();
            Dictionary<string, List<string>> objectTypeSupers = new ();

            foreach (string sourceFilePath in Directory.GetFiles(sourceRoot, $"*{fileSuffix}"))
            {
                Preload(deserializer, sourceFilePath, objectTypeComponentsAndProperties, objectTypeSupers);
            }

            Console.WriteLine();

            foreach (string sourceFilePath in Directory.GetFiles(sourceRoot, $"*{fileSuffix}"))
            {
                Dedup(deserializer, sourceFilePath, destRoot, objectTypeComponentsAndProperties, objectTypeSupers);
            }
        }

        private static void Preload(IDeserializer deserializer, string sourceFilePath, Dictionary<string, HashSet<string>> objectTypeComponentsAndProperties, Dictionary<string, List<string>> objectTypeSupers)
        {
            string yamlFileName = Path.GetFileName(sourceFilePath);

            Console.WriteLine($"Preloading file {yamlFileName}");

            string sourceFileText = File.ReadAllText(sourceFilePath);

            OpcUaDigest opcUaDigest = deserializer.Deserialize<OpcUaDigest>(sourceFileText);

            if (opcUaDigest.DefinedTypes != null)
            {
                foreach (KeyValuePair<string, OpcUaDefinedType> definedType in opcUaDigest.DefinedTypes)
                {
                    HashSet<string> componentsAndProperties = new ();
                    foreach (OpcUaContent content in definedType.Value.Contents)
                    {
                        if (content.Relationship == "HasComponent" || content.Relationship == "HasProperty")
                        {
                            componentsAndProperties.Add(DequalifyBrowseName(content.DefinedType.BrowseName));
                        }
                    }

                    List<string> superTypes = new ();
                    foreach (OpcUaContent content in definedType.Value.Contents)
                    {
                        if (content.Relationship == "HasSubtype_reverse")
                        {
                            if (TryGetQualifiedNameFromDefinedType(content.DefinedType, out string superType))
                            {
                                superTypes.Add(superType);
                            }
                        }
                    }

                    TryGetQualifiedNameFromDefinedType(definedType.Value, out string objectType);

                    objectTypeComponentsAndProperties[objectType] = componentsAndProperties;
                    objectTypeSupers[objectType] = superTypes;
                }
            }
        }

        private static bool TryGetQualifiedNameFromDefinedType(OpcUaDefinedType definedType, out string qualifiedName)
        {
            if (!definedType.NodeId.Contains(':'))
            {
                qualifiedName = string.Empty;
                return false;
            }

            qualifiedName = $"{definedType.NodeId.Substring(0, definedType.NodeId.IndexOf(':'))}:{definedType.BrowseName}";
            return true;
        }

        private static bool DoesAncestorHaveComponentOrProperty(string objectType, string componentOrPropertyName, Dictionary<string, HashSet<string>> objectTypeComponentsAndProperties, Dictionary<string, List<string>> objectTypeSupers)
        {
            foreach (string supertype in objectTypeSupers[objectType])
            {
                if (objectTypeComponentsAndProperties[supertype].Contains(componentOrPropertyName) || DoesAncestorHaveComponentOrProperty(supertype, componentOrPropertyName, objectTypeComponentsAndProperties, objectTypeSupers))
                {
                    return true;
                }
            }

            return false;
        }


        private static IEnumerable<OpcUaContent> DedupedContents(OpcUaDefinedType definedType, Dictionary<string, HashSet<string>> objectTypeComponentsAndProperties, Dictionary<string, List<string>> objectTypeSupers)
        {
            TryGetQualifiedNameFromDefinedType(definedType, out string objectType);

            foreach (OpcUaContent content in definedType.Contents)
            {
                if (definedType.NodeType != "UAObjectType" || (content.Relationship != "HasComponent" && content.Relationship != "HasProperty"))
                {
                    yield return content;
                }
                else if (!DoesAncestorHaveComponentOrProperty(objectType, DequalifyBrowseName(content.DefinedType.BrowseName), objectTypeComponentsAndProperties, objectTypeSupers))
                {
                    yield return content;
                }
            }
        }

        public static void Dedup(IDeserializer deserializer, string sourceFilePath, string destRoot, Dictionary<string, HashSet<string>> objectTypeComponentsAndProperties, Dictionary<string, List<string>> objectTypeSupers)
        {
            string yamlFileName = Path.GetFileName(sourceFilePath);

            Console.WriteLine($"Deduping file {yamlFileName}");

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
                        outputFile.WriteLine();
                        outputFile.WriteLine($"  {definedType.Key}:");
                        Visit(outputFile, 1, definedType.Value, objectTypeComponentsAndProperties, objectTypeSupers);
                    }
                }
            }
        }

        private static void Visit(StreamWriter outputFile, int depth, OpcUaDefinedType definedType, Dictionary<string, HashSet<string>> objectTypeComponentsAndProperties, Dictionary<string, List<string>> objectTypeSupers)
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

            foreach (OpcUaContent content in DedupedContents(definedType, objectTypeComponentsAndProperties, objectTypeSupers))
            {
                outputFile.WriteLine($"{currentIndent}- {content.Relationship}:");
                Visit(outputFile, depth + 1, content.DefinedType, objectTypeComponentsAndProperties, objectTypeSupers);
            }
        }

        private static string DequalifyBrowseName(string browseName) => browseName.Substring(browseName.IndexOf(':') + 1);
    }
}
