namespace Opc2Yaml
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml;

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

        private static readonly string[] sourceFilePrefixes = new string[] { "Opc.Ua.", "Opc." };
        private static readonly string[] typeNodeNames = new string[] { "UAVariableType", "UAObjectType" };
        private static readonly string[] terminalRefTypes = new string[] { "FromState", "ToState" };

        private static readonly Regex nodeIdRegex = new Regex(@"^(?:ns=(\d+);)?i=(\d+)$", RegexOptions.Compiled);

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
            ManagedXmlDocument coreSpecDoc = new ManagedXmlDocument(coreSpecFilePath);
            Dictionary<string, string> coreAliases = GetCoreAliases(coreSpecDoc);

            Dictionary<string, (string, string)> coreTypeNames = new ();
            PopulateOtherTypeNames(coreTypeNames, coreSpecDoc, null);

            string coreOutFileName = $"{destFilePrefix}{coreSpecName}{destFileSuffix}";
            string coreOutFilePath = Path.Combine(destRoot, coreOutFileName);
            Console.WriteLine($"  {coreSpecFileName} => {coreOutFileName}");

            using (StreamWriter outputFile = new StreamWriter(coreOutFilePath))
            {
                outputFile.WriteLine("DataTypes:");
                RecordDataTypes(coreAliases, null, coreSpecDoc, null, 0, outputFile);
            }

            Dictionary<string, SpecFile> specFiles = GetSpecFiles(sourceRoot, singleSpecName);
            foreach (KeyValuePair<string, SpecFile> specFile in specFiles)
            {
                Dictionary<string, string> dataTypeAliases = new ();
                Dictionary<string, (string, string)> otherTypeNames = new();
                foreach (ManagedXmlDocument xmlDoc in EnumerateRequiredModels(specFile.Value.SpecName, specFiles))
                {
                    Dictionary<string, string> namespaceMap = GetNamespaceMap(xmlDoc);
                    PopulateDataTypeAliases(dataTypeAliases, xmlDoc, namespaceMap);
                    PopulateOtherTypeNames(otherTypeNames, xmlDoc, namespaceMap);
                }

                string outFileName = $"{destFilePrefix}{specFile.Value.SpecName}{destFileSuffix}";
                string outFilePath = Path.Combine(destRoot, outFileName);
                Console.WriteLine($"  {specFile.Value.FileName} => {outFileName}");

                using (StreamWriter outputFile = new StreamWriter(outFilePath))
                {
                    outputFile.WriteLine("DataTypes:");

                    foreach (ManagedXmlDocument xmlDoc in EnumerateRequiredModels(specFile.Value.SpecName, specFiles))
                    {
                        Dictionary<string, string> namespaceMap = GetNamespaceMap(xmlDoc);
                        RecordDataTypes(coreAliases, dataTypeAliases, xmlDoc, namespaceMap, 0, outputFile);
                    }

                    string specFilePath = Path.Combine(specFile.Value.FolderPath, specFile.Value.FileName);
                    RecordDefinitions(coreAliases, coreTypeNames, otherTypeNames, dataTypeAliases, specFilePath, outputFile);
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
                    string specName = GetSpecName(specFileName);

                    if (singleSpecName == null || specName == singleSpecName)
                    {
                        specFiles[specName.ToLower()] = new SpecFile { SpecName = specName, FileName = specFileName, FolderPath = specFolderPath };
                    }
                }
            }

            return specFiles;
        }

        private static string GetSpecName(string specFileName)
        {
            foreach (string sourceFilePrefix in sourceFilePrefixes)
            {
                if (specFileName.StartsWith(sourceFilePrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    return specFileName.Substring(sourceFilePrefix.Length, specFileName.Length - sourceFilePrefix.Length - sourceFileSuffix.Length);
                }
            }

            return specFileName.Substring(0, specFileName.Length - sourceFileSuffix.Length);
        }

        private static IEnumerable<ManagedXmlDocument> EnumerateRequiredModels(string rawSpecName, Dictionary<string, SpecFile> specFiles, HashSet<string>? visitedSpecs = null)
        {
            string specName = rawSpecName.ToLower();
            if (visitedSpecs == null)
            {
                visitedSpecs = new HashSet<string>();
            }
            else if (visitedSpecs.Contains(specName))
            {
                yield break;
            }

            visitedSpecs.Add(specName);

            SpecFile specFile = specFiles[specName];

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

                foreach (ManagedXmlDocument xmlDoc in EnumerateRequiredModels(GetModelNameFromUri(modelUri), specFiles, visitedSpecs))
                {
                    yield return xmlDoc;
                }
            }
        }

        private static Dictionary<string, string> GetCoreAliases(ManagedXmlDocument coreSpecDoc)
        {
            Dictionary<string, string> coreAliases = new ();
            foreach (XmlNode aliasNode in coreSpecDoc.RootElement.SelectNodes("//*[@Alias]")!)
            {
                coreAliases[TranslateNodeId(aliasNode.InnerText, null)] = aliasNode.Attributes!["Alias"]!.Value;
            }

            PopulateDataTypeAliases(coreAliases, coreSpecDoc, null);

            return coreAliases;
        }

        private static void RecordDefinitions(Dictionary<string, string> coreAliases, Dictionary<string, (string, string)> coreTypeNames, Dictionary<string, (string, string)> otherTypeNames, Dictionary<string, string> dataTypeAliases, string specFilePath, StreamWriter outputFile)
        {
            ManagedXmlDocument xmlDoc = new ManagedXmlDocument(specFilePath);
            Dictionary<string, string> namespaceMap = GetNamespaceMap(xmlDoc);

            outputFile.WriteLine();
            outputFile.WriteLine("DefinedTypes:");
            foreach (string typeNodeName in typeNodeNames)
            {
                foreach (XmlNode otNode in xmlDoc.RootElement.SelectNodes($"//opc:{typeNodeName}", xmlDoc.NamespaceManager)!)
                {
                    string rawBrowseName = otNode.Attributes!["BrowseName"]!.Value;
                    string browseName = TrimBrowseName(rawBrowseName);
                    outputFile.WriteLine();
                    outputFile.WriteLine($"  {browseName}:");
                    VisitNodes(coreAliases, coreTypeNames, otherTypeNames, dataTypeAliases, namespaceMap, xmlDoc.NamespaceManager, otNode, 1, outputFile, ExpansionCondition.ExpandAll);
                }
            }
        }

        private static string GetModelNameFromUri(string modelUri) => modelUri switch
        {
            "http://fdi-cooperation.com/OPCUA/FDI5/" => "FDI5",
            "http://fdi-cooperation.com/OPCUA/FDI7/" => "FDI7",
            "http://opcfoundation.org/UA/AML/" => "AMLBaseTypes",
            "http://opcfoundation.org/UA/Dictionary/IRDI" => "IRDI",
            "http://opcfoundation.org/UA/ISA95-JOBCONTROL_V2/" => "isa95-jobcontrol",
            "http://sercos.org/UA/" => "sercos",
            "http://vdma.org/UA/LaserSystem-Example/" => "LaserSystem-Example",
            "http://www.OPCFoundation.org/UA/2013/01/ISA95" => "ISA95",
            string uri => uri.Substring(modelUriPrefix.Length, uri.Length - modelUriPrefix.Length - (uri.EndsWith('/') ? 1 : 0)).Replace('/', '.'),
        };

        private static Dictionary<string, string> GetNamespaceMap(ManagedXmlDocument specDoc)
        {
            XmlNode? namespaceNode = specDoc.RootElement.SelectSingleNode("descendant::opc:NamespaceUris", specDoc.NamespaceManager);
            ArgumentNullException.ThrowIfNull(namespaceNode);

            Dictionary<string, string> namespaceMap = new ();

            int ix = 0;
            foreach (XmlNode dtNode in namespaceNode.SelectNodes("child::opc:Uri", specDoc.NamespaceManager)!)
            {
                ++ix;
                namespaceMap[ix.ToString()] = GetModelNameFromUri(dtNode.InnerText);
            }

            return namespaceMap;
        }

        private static string ResolveDataType(string rawDataType, Dictionary<string, string> coreAliases, Dictionary<string, string>? dataTypeAliases, Dictionary<string, string>? namespaceMap)
        {
            string nodeId = TranslateNodeId(rawDataType, namespaceMap);
            if (coreAliases.TryGetValue(nodeId, out string? dataType))
            {
                return dataType;
            }
            else if (dataTypeAliases != null && dataTypeAliases.TryGetValue(nodeId, out dataType))
            {
                return dataType;
            }
            else
            {
                return nodeId;
            }
        }

        private static void PopulateDataTypeAliases(Dictionary<string, string> dataTypeAliases, ManagedXmlDocument specDoc, Dictionary<string, string>? namespaceMap)
        {
            foreach (XmlNode dtNode in specDoc.RootElement.SelectNodes("//opc:UADataType", specDoc.NamespaceManager)!)
            {
                string rawNodeId = dtNode.Attributes!["NodeId"]!.Value;
                string rawBrowseName = dtNode.Attributes!["BrowseName"]!.Value;

                dataTypeAliases[TranslateNodeId(rawNodeId, namespaceMap)] = TrimBrowseName(rawBrowseName);
            }
        }

        private static void PopulateOtherTypeNames(Dictionary<string, (string, string)> otherTypeNames, ManagedXmlDocument specDoc, Dictionary<string, string>? namespaceMap)
        {
            foreach (XmlNode dtNode in specDoc.RootElement.SelectNodes($"child::*[@BrowseName]")!)
            {
                string rawNodeId = dtNode.Attributes!["NodeId"]!.Value;
                string rawBrowseName = dtNode.Attributes!["BrowseName"]!.Value;

                otherTypeNames[TranslateNodeId(rawNodeId, namespaceMap)] = (dtNode.Name, TrimBrowseName(rawBrowseName));
            }
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

        private static string TrimBrowseName(string rawBrowseName) => rawBrowseName.Contains(':') ? rawBrowseName.Substring(rawBrowseName.IndexOf(':') + 1) : rawBrowseName;

        private static void RecordDataTypes(Dictionary<string, string> coreAliases, Dictionary<string, string>? dataTypeAliases, ManagedXmlDocument xmlDoc, Dictionary<string, string>? namespaceMap, int depth, StreamWriter outputFile)
        {
            string currentIndent = new string(' ', depth * 2);

            foreach (XmlNode dtNode in xmlDoc.RootElement.SelectNodes("//opc:UADataType", xmlDoc.NamespaceManager)!)
            {
                string rawNodeId = dtNode.Attributes!["NodeId"]!.Value;
                string rawBrowseName = dtNode.Attributes!["BrowseName"]!.Value;

                string nodeId = TranslateNodeId(rawNodeId, namespaceMap);
                string browseName = TrimBrowseName(rawBrowseName);

                XmlNode? someFieldNode = dtNode.SelectSingleNode("descendant::opc:Field", xmlDoc.NamespaceManager);
                if (someFieldNode?.Attributes!["Value"] != null)
                {
                    outputFile.WriteLine($"{currentIndent}- {dtNode.Name}: [ {nodeId}, {browseName} ]");
                    outputFile.WriteLine($"{currentIndent}  Enums:");

                    foreach (XmlNode fieldNode in dtNode.SelectNodes("descendant::opc:Field", xmlDoc.NamespaceManager)!)
                    {
                        outputFile.WriteLine($"{currentIndent}    {fieldNode.Attributes!["Name"]!.Value}: {fieldNode.Attributes!["Value"]!.Value}");
                    }
                }
                else if (someFieldNode?.Attributes!["DataType"] != null)
                {
                    outputFile.WriteLine($"{currentIndent}- {dtNode.Name}: [ {nodeId}, {browseName} ]");
                    outputFile.WriteLine($"{currentIndent}  Fields:");

                    foreach (XmlNode fieldNode in dtNode.SelectNodes("descendant::opc:Field", xmlDoc.NamespaceManager)!)
                    {
                        string dataType = "null";
                        XmlAttribute? dataTypeAttr = fieldNode.Attributes!["DataType"];
                        if (dataTypeAttr != null)
                        {
                            dataType = ResolveDataType(dataTypeAttr.Value, coreAliases, dataTypeAliases, namespaceMap);
                        }

                        XmlAttribute? valueRankAttr = fieldNode.Attributes!["ValueRank"];
                        int valueRank = int.Parse(valueRankAttr?.Value ?? "0");

                        outputFile.WriteLine($"{currentIndent}    {fieldNode.Attributes!["Name"]!.Value}: [ {dataType}, {valueRank} ]");
                    }
                }
                else
                {
                    bool doneHeading = false;
                    foreach (XmlNode refNode in dtNode.SelectNodes("descendant::opc:Reference", xmlDoc.NamespaceManager)!)
                    {
                        if (refNode?.Attributes!["ReferenceType"]?.Value == "HasSubtype" && refNode?.Attributes!["IsForward"]?.Value == "false")
                        {
                            if (!doneHeading)
                            {
                                outputFile.WriteLine($"{currentIndent}- {dtNode.Name}: [ {nodeId}, {browseName} ]");
                                outputFile.WriteLine($"{currentIndent}  Bases:");
                                doneHeading = true;
                            }

                            string baseType = ResolveDataType(refNode.InnerText, coreAliases, dataTypeAliases, namespaceMap);
                            outputFile.WriteLine($"{currentIndent}  - {baseType}");
                        }
                    }
                }
            }
        }

        private static void VisitNodes(Dictionary<string, string> coreAliases, Dictionary<string, (string, string)> coreTypeNames, Dictionary<string, (string, string)> otherTypeNames, Dictionary<string, string> dataTypeAliases, Dictionary<string, string> namespaceMap, XmlNamespaceManager nsmgr, XmlNode xmlNode, int depth, StreamWriter outputFile, ExpansionCondition expansionCondition)
        {
            string currentIndent = new string(' ', depth * 2);

            string rawNodeId = xmlNode.Attributes!["NodeId"]!.Value;
            string rawBrowseName = xmlNode.Attributes!["BrowseName"]!.Value;

            string nodeId = TranslateNodeId(rawNodeId, namespaceMap);
            string browseName = TrimBrowseName(rawBrowseName);

            string dataTypeStr = string.Empty;
            string valueRankStr = string.Empty;
            string accessLevelStr = string.Empty;
            XmlAttribute? dataTypeAttr = xmlNode.Attributes!["DataType"];
            if (dataTypeAttr != null)
            {
                dataTypeStr = $", {ResolveDataType(dataTypeAttr.Value, coreAliases, dataTypeAliases, namespaceMap)}";

                XmlAttribute? valueRankAttr = xmlNode.Attributes!["ValueRank"];
                int valueRank = int.Parse(valueRankAttr?.Value ?? "0");
                valueRankStr = $", {valueRank}";

                XmlAttribute? accessLevelAttr = xmlNode.Attributes!["AccessLevel"];
                int accessLevel = int.Parse(accessLevelAttr?.Value ?? "0");
                accessLevelStr = $", {accessLevel}";
            }

            outputFile.WriteLine($"{currentIndent}- [ {xmlNode.Name}, {nodeId}, {browseName}{dataTypeStr}{valueRankStr}{accessLevelStr} ]");

            if (expansionCondition == ExpansionCondition.ExpandNone ||
                expansionCondition == ExpansionCondition.ExpandUnlessType && typeNodeNames.Contains(xmlNode.Name))
            {
                return;
            }

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
                    string dataType = ResolveDataType(dataTypeAlias, coreAliases, dataTypeAliases, namespaceMap);

                    int valueRank = int.Max(0, int.Parse(argNode.SelectSingleNode("child::uax:ValueRank", nsmgr)?.InnerText ?? "0"));

                    outputFile.WriteLine($"{currentIndent}    {argName}: [ {dataType}, {valueRank} ]");
                }
            }

            foreach (XmlNode node in xmlNode.SelectNodes($"descendant::*[@ReferenceType]", nsmgr)!)
            {
                if (node.Attributes!["IsForward"]?.Value == "false")
                {
                    continue;
                }

                string rawReferenceType = node.Attributes!["ReferenceType"]!.Value;
                string referenceType = ResolveDataType(rawReferenceType, coreAliases, dataTypeAliases, namespaceMap);

                XmlNode? subNode = xmlNode.SelectSingleNode($"//*[@NodeId='{node.InnerText}']", nsmgr);
                if (subNode != null)
                {
                    outputFile.WriteLine($"{currentIndent}- {referenceType}:");
                    VisitNodes(coreAliases, coreTypeNames, otherTypeNames, dataTypeAliases, namespaceMap, nsmgr, subNode, depth + 1, outputFile, terminalRefTypes.Contains(referenceType) ? ExpansionCondition.ExpandNone : ExpansionCondition.ExpandUnlessType);
                }
                else
                {
                    string translatedNodeId = TranslateNodeId(node.InnerText, namespaceMap);
                    (string, string) otherTypeName;
                    if (coreTypeNames.TryGetValue(translatedNodeId, out otherTypeName) || otherTypeNames.TryGetValue(translatedNodeId, out otherTypeName))
                    {
                        outputFile.WriteLine($"{currentIndent}- {referenceType}:");
                        outputFile.WriteLine($"{currentIndent}  - [ {otherTypeName.Item1}, {translatedNodeId}, {otherTypeName.Item2} ]");
                    }
                }
            }
        }
    }
}
