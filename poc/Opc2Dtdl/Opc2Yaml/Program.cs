namespace Opc2Yaml
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml;
    using SpecMapper;

    public enum ExpansionCondition
    {
        ExpandAll,
        ExpandUnlessType,
        ExpandNone,
    }

    public struct SpecFile
    {
        public string SpecName;
        public string FileName;
        public string FolderPath;
    }

    public class Program
    {
        private const string coreSubFolderName = "Schema";
        private const string coreSpecFileName = "Opc.Ua.NodeSet2.Services.xml";
        private const string coreSpecName = "OpcUaCore";
        private const string sourceFileSuffix = ".NodeSet2.xml";
        private const string destFilePrefix = "";
        private const string destFileSuffix = ".digest.yaml";
        private const string modelUriPrefix = "http://opcfoundation.org/UA/";

        private static readonly string[] typeNodeNames = new string[] { "UAVariableType", "UAObjectType" };
        private static readonly string[] terminalRefTypes = new string[] { "FromState", "ToState" };

        private static readonly Regex nodeIdRegex = new Regex(@"^(?:ns=(\d+);)?i=(\d+)$", RegexOptions.Compiled);

        private static Dictionary<string, string> nodeIdToReferenceTypeNameMap = new();
        private static Dictionary<string, (string, string)> resolvedNodeIdToNodeTypeAndBrowseNameMap = new();

        private static void PopulateMaps(string coreSpecFilePath, Dictionary<string, SpecFile> specFiles)
        {
            ManagedXmlDocument coreSpecDoc = new ManagedXmlDocument(coreSpecFilePath);
            foreach (XmlNode dtNode in coreSpecDoc.RootElement.SelectNodes("//opc:UAReferenceType", coreSpecDoc.NamespaceManager)!)
            {
                string rawNodeId = dtNode.Attributes!["NodeId"]!.Value;
                string browseName = dtNode.Attributes!["BrowseName"]!.Value;

                nodeIdToReferenceTypeNameMap[TranslateNodeId(rawNodeId, null)] = browseName;
            }

            PopulateMapFromSpecDoc(coreSpecDoc);

            foreach (KeyValuePair<string, SpecFile> specFile in specFiles)
            {
                string specFilePath = Path.Combine(specFile.Value.FolderPath, specFile.Value.FileName);
                ManagedXmlDocument specDoc = new ManagedXmlDocument(specFilePath);

                PopulateMapFromSpecDoc(specDoc);
            }
        }

        private static void PopulateMapFromSpecDoc(ManagedXmlDocument specDoc)
        {
            Dictionary<string, string> namespaceMap = GetNamespaceMap(specDoc);

            foreach (XmlNode dtNode in specDoc.RootElement.SelectNodes("child::*[@BrowseName]", specDoc.NamespaceManager)!)
            {
                string rawNodeId = dtNode.Attributes!["NodeId"]!.Value;
                string browseName = dtNode.Attributes!["BrowseName"]!.Value;

                resolvedNodeIdToNodeTypeAndBrowseNameMap[TranslateNodeId(rawNodeId, namespaceMap)] = (dtNode.Name, MapBrowseNameViaNodeId(browseName, rawNodeId, namespaceMap));
            }
        }

        private static void ConvertCoreSpec(string coreSpecFilePath, string destRoot)
        {
            ManagedXmlDocument coreSpecDoc = new ManagedXmlDocument(coreSpecFilePath);

            string coreOutFileName = $"{destFilePrefix}{coreSpecName}{destFileSuffix}";
            string coreOutFilePath = Path.Combine(destRoot, coreOutFileName);
            Console.WriteLine($"  {coreSpecFileName} => {coreOutFileName}");

            string specUri = coreSpecDoc.RootElement.SelectSingleNode("//opc:Model", coreSpecDoc.NamespaceManager)!.Attributes!["ModelUri"]!.Value;

            using (StreamWriter outputFile = new StreamWriter(coreOutFilePath))
            {
                outputFile.WriteLine($"SpecUri: {specUri}");
                outputFile.WriteLine();

                outputFile.WriteLine("DataTypes:");
                RecordDataTypes(coreSpecDoc, outputFile);
                outputFile.WriteLine();

                RecordDefinitions(coreSpecDoc, outputFile);
            }
        }

        public static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("usage: Opc2Yaml <SOURCE_ROOT> <DEST_ROOT> [ <SPEC_NAME> ]");
                return;
            }

            string sourceRoot = args[0];
            string destRoot = args[1];
            string? singleSpecName = args.Length > 2 ? args[2] : null;

            if (!Directory.Exists(destRoot))
            {
                Directory.CreateDirectory(destRoot);
            }

            string coreSpecFilePath = Path.Combine(sourceRoot, coreSubFolderName, coreSpecFileName);
            Dictionary<string, SpecFile> specFiles = GetSpecFiles(sourceRoot, singleSpecName);

            PopulateMaps(coreSpecFilePath, specFiles);

            ConvertCoreSpec(coreSpecFilePath, destRoot);

            foreach (KeyValuePair<string, SpecFile> specFile in specFiles)
            {
                string specFilePath = Path.Combine(specFile.Value.FolderPath, specFile.Value.FileName);

                string outFileName = $"{destFilePrefix}{specFile.Value.SpecName}{destFileSuffix}";
                string outFilePath = Path.Combine(destRoot, outFileName);
                Console.WriteLine($"  {specFile.Value.FileName} => {outFileName}");

                using (StreamWriter outputFile = new StreamWriter(outFilePath))
                {
                    ManagedXmlDocument specDoc = new ManagedXmlDocument(specFilePath);
                    string specUri = specDoc.RootElement.SelectSingleNode("//opc:Model", specDoc.NamespaceManager)!.Attributes!["ModelUri"]!.Value;

                    outputFile.WriteLine($"SpecUri: {specUri}");
                    outputFile.WriteLine();

                    outputFile.WriteLine("DataTypes:");
                    foreach (ManagedXmlDocument reqSpecDoc in EnumerateRequiredModels(specFile.Value.SpecName, specFiles))
                    {
                        RecordDataTypes(reqSpecDoc, outputFile);
                    }

                    outputFile.WriteLine();
                    RecordDefinitions(specDoc, outputFile);
                }
            }
        }

        private static Dictionary<string, SpecFile> GetSpecFiles(string sourceRoot, string? singleSpecName)
        {
            Dictionary<string, SpecFile> specFiles = new();

            foreach (string specFolderPath in Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(specFolderPath) == coreSubFolderName)
                {
                    continue;
                }

                foreach (string specFilePath in Directory.GetFiles(specFolderPath, $"*{sourceFileSuffix}"))
                {
                    string specFileName = Path.GetFileName(specFilePath);
                    string specName = GetSpecName(specFilePath);

                    if (singleSpecName == null || specName == singleSpecName)
                    {
                        specFiles[specName.ToLower()] = new SpecFile { SpecName = specName, FileName = specFileName, FolderPath = specFolderPath };
                    }
                }
            }

            return specFiles;
        }

        private static string GetSpecName(string specFilePath)
        {
            ManagedXmlDocument specDoc = new ManagedXmlDocument(specFilePath);

            return SpecMapper.GetSpecNameFromUri(specDoc.RootElement.SelectSingleNode("//opc:Model", specDoc.NamespaceManager)!.Attributes!["ModelUri"]!.Value);
        }

        private static IEnumerable<ManagedXmlDocument> EnumerateRequiredModels(string specName, Dictionary<string, SpecFile> specFiles, HashSet<string>? visitedSpecs = null)
        {
            string lowerSpecName = specName.ToLower();
            if (visitedSpecs == null)
            {
                visitedSpecs = new HashSet<string>();
            }
            else if (visitedSpecs.Contains(lowerSpecName))
            {
                yield break;
            }

            visitedSpecs.Add(lowerSpecName);

            SpecFile specFile = specFiles[lowerSpecName];

            string specFilePath = Path.Combine(specFile.FolderPath, specFile.FileName);
            ManagedXmlDocument specDoc = new ManagedXmlDocument(specFilePath);

            yield return specDoc;

            foreach (XmlNode dtNode in specDoc.RootElement.SelectNodes("//opc:RequiredModel", specDoc.NamespaceManager)!)
            {
                string modelUri = dtNode.Attributes!["ModelUri"]!.Value;
                if (modelUri == modelUriPrefix)
                {
                    continue;
                }

                foreach (ManagedXmlDocument xmlDoc in EnumerateRequiredModels(SpecMapper.GetSpecNameFromUri(modelUri), specFiles, visitedSpecs))
                {
                    yield return xmlDoc;
                }
            }
        }

        private static Dictionary<string, string> GetLocalAliasToResolvedNodeIdMap(ManagedXmlDocument specDoc, Dictionary<string, string>? namespaceMap)
        {
            Dictionary<string, string> localAliasToResolvedNodeIdMap = new();
            foreach (XmlNode aliasNode in specDoc.RootElement.SelectNodes("//*[@Alias]")!)
            {
                localAliasToResolvedNodeIdMap[aliasNode.Attributes!["Alias"]!.Value] = TranslateNodeId(aliasNode.InnerText, namespaceMap);
            }

            return localAliasToResolvedNodeIdMap;
        }

        private static void RecordDefinitions(ManagedXmlDocument specDoc, StreamWriter outputFile)
        {
            Dictionary<string, string> namespaceMap = GetNamespaceMap(specDoc);
            Dictionary<string, string> localAliasToResolvedNodeIdMap = GetLocalAliasToResolvedNodeIdMap(specDoc, namespaceMap);

            outputFile.WriteLine("DefinedTypes:");
            foreach (string typeNodeName in typeNodeNames)
            {
                foreach (XmlNode otNode in specDoc.RootElement.SelectNodes($"//opc:{typeNodeName}", specDoc.NamespaceManager)!)
                {
                    string rawBrowseName = otNode.Attributes!["BrowseName"]!.Value;
                    string browseName = MapBrowseName(rawBrowseName, namespaceMap);
                    outputFile.WriteLine();
                    outputFile.WriteLine($"  {browseName}:");
                    VisitNodes(namespaceMap, localAliasToResolvedNodeIdMap, specDoc.NamespaceManager, otNode, 1, outputFile, ExpansionCondition.ExpandAll);
                }
            }
        }

        private static Dictionary<string, string> GetNamespaceMap(ManagedXmlDocument specDoc)
        {
            Dictionary<string, string> namespaceMap = new();

            XmlNode? namespaceNode = specDoc.RootElement.SelectSingleNode("descendant::opc:NamespaceUris", specDoc.NamespaceManager);
            if (namespaceNode == null)
            {
                return namespaceMap;
            }

            int ix = 0;
            foreach (XmlNode dtNode in namespaceNode.SelectNodes("child::opc:Uri", specDoc.NamespaceManager)!)
            {
                ++ix;
                namespaceMap[ix.ToString()] = SpecMapper.GetSpecNameFromUri(dtNode.InnerText);
            }

            return namespaceMap;
        }

        private static string ResolveDataType(string rawDataType, Dictionary<string, string> localAliasToResolvedNodeIdMap, Dictionary<string, string>? namespaceMap)
        {
            string? resolvedNodeId;
            if (!localAliasToResolvedNodeIdMap.TryGetValue(rawDataType, out resolvedNodeId))
            {
                resolvedNodeId = TranslateNodeId(rawDataType, namespaceMap);
            }

            (string, string) nodeTypeAndBrowseName;
            return resolvedNodeIdToNodeTypeAndBrowseNameMap.TryGetValue(resolvedNodeId, out nodeTypeAndBrowseName) ? nodeTypeAndBrowseName.Item2 : resolvedNodeId;
        }

        private static string TranslateNodeId(string rawNodeId, Dictionary<string, string>? namespaceMap)
        {
            Match nodeIdMatch = nodeIdRegex.Match(rawNodeId);
            if (!nodeIdMatch.Success)
            {
                return rawNodeId;
            }

            string nsPrefix = string.Empty;
            if (nodeIdMatch.Groups[1].Success)
            {
                ArgumentNullException.ThrowIfNull(namespaceMap);
                nsPrefix = $"{namespaceMap[nodeIdMatch.Groups[1].Captures[0].Value]}:";
            }

            return $"{nsPrefix}{nodeIdMatch.Groups[2].Captures[0].Value}";
        }

        private static string MapBrowseName(string rawBrowseName, Dictionary<string, string>? namespaceMap) =>
            namespaceMap != null && rawBrowseName.Contains(':') ? $"{namespaceMap[rawBrowseName.Substring(0, rawBrowseName.IndexOf(':'))]}{rawBrowseName.Substring(rawBrowseName.IndexOf(':'))}" : rawBrowseName;

        private static string MapBrowseNameViaNodeId(string rawBrowseName, string rawNodeId, Dictionary<string, string>? namespaceMap)
        {
            if (rawBrowseName.Contains(':'))
            {
                string namespaceIndex = rawBrowseName.Substring(0, rawBrowseName.IndexOf(':'));
                string unqualifiedBrowseName = rawBrowseName.Substring(rawBrowseName.IndexOf(':'));
                if (namespaceIndex == "0")
                {
                    return unqualifiedBrowseName;
                }

                ArgumentNullException.ThrowIfNull(namespaceMap);
                return $"{namespaceMap[namespaceIndex]}{unqualifiedBrowseName}";
            }

            Match nodeIdMatch = nodeIdRegex.Match(rawNodeId);
            if (!nodeIdMatch.Success || !nodeIdMatch.Groups[1].Success)
            {
                return rawBrowseName;
            }

            ArgumentNullException.ThrowIfNull(namespaceMap);
            return $"{namespaceMap[nodeIdMatch.Groups[1].Captures[0].Value]}:{rawBrowseName}";
        }

        private static void RecordDataTypes(ManagedXmlDocument specDoc, StreamWriter outputFile)
        {
            Dictionary<string, string> namespaceMap = GetNamespaceMap(specDoc);
            Dictionary<string, string> localAliasToResolvedNodeIdMap = GetLocalAliasToResolvedNodeIdMap(specDoc, namespaceMap);

            foreach (XmlNode dtNode in specDoc.RootElement.SelectNodes("//opc:UADataType", specDoc.NamespaceManager)!)
            {
                string rawNodeId = dtNode.Attributes!["NodeId"]!.Value;
                string rawBrowseName = dtNode.Attributes!["BrowseName"]!.Value;

                string nodeId = TranslateNodeId(rawNodeId, namespaceMap);
                string browseName = MapBrowseNameViaNodeId(rawBrowseName, rawNodeId, namespaceMap);

                string? displayName = dtNode.SelectSingleNode("descendant::opc:DisplayName", specDoc.NamespaceManager)?.InnerText;
                string? description = dtNode.SelectSingleNode("descendant::opc:Description", specDoc.NamespaceManager)?.InnerText;

                XmlNode? someFieldNode = dtNode.SelectSingleNode("descendant::opc:Field", specDoc.NamespaceManager);
                if (someFieldNode?.Attributes!["Value"] != null)
                {
                    outputFile.WriteLine($"- {dtNode.Name}: [ {nodeId}, {browseName} ]");
                    RecordDisplayNameAndDescription(outputFile, displayName, description);
                    outputFile.WriteLine($"  Enums:");

                    foreach (XmlNode fieldNode in dtNode.SelectNodes("descendant::opc:Field", specDoc.NamespaceManager)!)
                    {
                        outputFile.WriteLine($"    {fieldNode.Attributes!["Name"]!.Value}: {fieldNode.Attributes!["Value"]!.Value}");
                    }
                }
                else if (someFieldNode?.Attributes!["DataType"] != null)
                {
                    outputFile.WriteLine($"- {dtNode.Name}: [ {nodeId}, {browseName} ]");
                    RecordDisplayNameAndDescription(outputFile, displayName, description);
                    outputFile.WriteLine($"  Fields:");

                    foreach (XmlNode fieldNode in dtNode.SelectNodes("descendant::opc:Field", specDoc.NamespaceManager)!)
                    {
                        string dataType = "null";
                        XmlAttribute? dataTypeAttr = fieldNode.Attributes!["DataType"];
                        if (dataTypeAttr != null)
                        {
                            dataType = ResolveDataType(dataTypeAttr.Value, localAliasToResolvedNodeIdMap, namespaceMap);
                        }

                        XmlAttribute? valueRankAttr = fieldNode.Attributes!["ValueRank"];
                        int valueRank = int.Parse(valueRankAttr?.Value ?? "0");

                        outputFile.WriteLine($"    {fieldNode.Attributes!["Name"]!.Value}: [ {dataType}, {valueRank} ]");
                    }
                }
                else
                {
                    bool doneHeading = false;
                    foreach (XmlNode refNode in dtNode.SelectNodes("descendant::opc:Reference", specDoc.NamespaceManager)!)
                    {
                        if (refNode?.Attributes!["ReferenceType"]?.Value == "HasSubtype" && refNode?.Attributes!["IsForward"]?.Value == "false")
                        {
                            if (!doneHeading)
                            {
                                outputFile.WriteLine($"- {dtNode.Name}: [ {nodeId}, {browseName} ]");
                                outputFile.WriteLine($"  Bases:");
                                doneHeading = true;
                            }

                            string baseType = ResolveDataType(refNode.InnerText, localAliasToResolvedNodeIdMap, namespaceMap);
                            outputFile.WriteLine($"  - {baseType}");
                        }
                    }
                }
            }
        }

        private static void VisitNodes(Dictionary<string, string> namespaceMap, Dictionary<string, string> localAliasToResolvedNodeIdMap, XmlNamespaceManager nsmgr, XmlNode xmlNode, int depth, StreamWriter outputFile, ExpansionCondition expansionCondition)
        {
            string currentIndent = new string(' ', depth * 2);

            string rawNodeId = xmlNode.Attributes!["NodeId"]!.Value;
            string rawBrowseName = xmlNode.Attributes!["BrowseName"]!.Value;

            string nodeId = TranslateNodeId(rawNodeId, namespaceMap);
            string browseName = MapBrowseNameViaNodeId(rawBrowseName, rawNodeId, namespaceMap);

            string? displayName = xmlNode.SelectSingleNode("descendant::opc:DisplayName", nsmgr)?.InnerText;
            string? description = xmlNode.SelectSingleNode("descendant::opc:Description", nsmgr)?.InnerText;

            string dataTypeStr = string.Empty;
            string valueRankStr = string.Empty;
            string accessLevelStr = string.Empty;
            XmlAttribute? dataTypeAttr = xmlNode.Attributes!["DataType"];
            XmlAttribute? isAbstractAttr = xmlNode.Attributes!["IsAbstract"];
            if (dataTypeAttr != null)
            {
                dataTypeStr = $", {ResolveDataType(dataTypeAttr.Value, localAliasToResolvedNodeIdMap, namespaceMap)}";

                XmlAttribute? valueRankAttr = xmlNode.Attributes!["ValueRank"];
                int valueRank = int.Parse(valueRankAttr?.Value ?? "0");
                valueRankStr = $", {valueRank}";

                XmlAttribute? accessLevelAttr = xmlNode.Attributes!["AccessLevel"];
                int accessLevel = int.Parse(accessLevelAttr?.Value ?? "0");
                accessLevelStr = $", {accessLevel}";
            }
            else if (isAbstractAttr != null)
            {
                if (isAbstractAttr.Value == "true")
                {
                    dataTypeStr = ", Abstract";
                }
            }

            outputFile.WriteLine($"{currentIndent}- [ {xmlNode.Name}, {nodeId}, {browseName}{dataTypeStr}{valueRankStr}{accessLevelStr} ]");

            if (expansionCondition == ExpansionCondition.ExpandNone ||
                expansionCondition == ExpansionCondition.ExpandUnlessType && typeNodeNames.Contains(xmlNode.Name))
            {
                return;
            }

            RecordDisplayNameAndDescription(outputFile, displayName, description, currentIndent, asList: true);

            if (xmlNode.SelectSingleNode("descendant::uax:Argument", nsmgr) != null)
            {
                outputFile.WriteLine($"{currentIndent}- Arguments:");

                foreach (XmlNode argNode in xmlNode.SelectNodes("descendant::uax:Argument", nsmgr)!)
                {
                    string argName = argNode.SelectSingleNode("child::uax:Name", nsmgr)?.InnerText!;
                    if (argName == string.Empty)
                    {
                        argName = "\"\"";
                    }

                    string dataTypeAlias = argNode.SelectSingleNode("child::uax:DataType/child::uax:Identifier", nsmgr)?.InnerText!;
                    string dataType = ResolveDataType(dataTypeAlias, localAliasToResolvedNodeIdMap, namespaceMap);

                    int valueRank = int.Max(0, int.Parse(argNode.SelectSingleNode("child::uax:ValueRank", nsmgr)?.InnerText ?? "0"));

                    outputFile.WriteLine($"{currentIndent}    {argName}: [ {dataType}, {valueRank} ]");
                }
            }

            XmlNode? unitIdNode = xmlNode.SelectSingleNode("descendant::uax:UnitId", nsmgr);
            if (unitIdNode != null)
            {
                outputFile.WriteLine($"{currentIndent}- UnitId: {unitIdNode.InnerText}");
            }

            foreach (XmlNode node in xmlNode.SelectNodes($"descendant::*[@ReferenceType]", nsmgr)!)
            {
                bool reverseRef = node.Attributes!["IsForward"]?.Value == "false";
                string rawReferenceType = node.Attributes!["ReferenceType"]!.Value;
                if (!rawReferenceType.StartsWith("ns="))
                {
                    if (!nodeIdToReferenceTypeNameMap.TryGetValue(rawReferenceType, out string? namedReferenceType))
                    {
                        namedReferenceType = rawReferenceType;
                    }
                    string referenceType = namedReferenceType + (reverseRef ? "_reverse" : string.Empty);

                    XmlNode? subNode = xmlNode.SelectSingleNode($"//*[@NodeId='{node.InnerText}']", nsmgr);
                    if (subNode != null)
                    {
                        outputFile.WriteLine($"{currentIndent}- {referenceType}:");
                        VisitNodes(namespaceMap, localAliasToResolvedNodeIdMap, nsmgr, subNode, depth + 1, outputFile, (terminalRefTypes.Contains(referenceType) || reverseRef) ? ExpansionCondition.ExpandNone : ExpansionCondition.ExpandUnlessType);
                    }
                    else
                    {
                        string translatedNodeId = TranslateNodeId(node.InnerText, namespaceMap);

                        (string, string) nodeTypeAndBrowseName;
                        if (resolvedNodeIdToNodeTypeAndBrowseNameMap.TryGetValue(translatedNodeId, out nodeTypeAndBrowseName))
                        {
                            outputFile.WriteLine($"{currentIndent}- {referenceType}:");
                            outputFile.WriteLine($"{currentIndent}  - [ {nodeTypeAndBrowseName.Item1}, {translatedNodeId}, {nodeTypeAndBrowseName.Item2} ]");
                        }
                    }
                }
            }
        }

        private static void RecordDisplayNameAndDescription(StreamWriter outputFile, string? displayName, string? description, string currentIndent = "", bool asList = false)
        {
            string leader = asList ? "- " : "  ";

            if (displayName != null)
            {
                outputFile.WriteLine($"{currentIndent}{leader}DisplayName: \"{displayName}\"");
            }
            if (description != null)
            {
                outputFile.WriteLine($"{currentIndent}{leader}Description: \"{Regex.Replace(description.Replace(Environment.NewLine, " ").Replace("\\", "").Replace('\"', '\'').Trim(), @"\s+", " ")}\"");
            }
        }
    }
}
