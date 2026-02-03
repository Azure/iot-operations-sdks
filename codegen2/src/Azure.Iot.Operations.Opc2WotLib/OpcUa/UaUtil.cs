// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml;

    public static class UaUtil
    {
        public static string GetNodeIdString(int nsIndex, int nodeIndex)
        {
            return nsIndex > 0 ? $"ns={nsIndex};i={nodeIndex}" : $"i={nodeIndex}";
        }

        public static string GetNameString(int nsIndex, string name)
        {
            return nsIndex > 0 ? $"{nsIndex}:{name}" : name;
        }

        public static OpcUaNodeId ParseTypeString(string typeString, OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap)
        {
            return modelInfo.AliasMap.TryGetValue(typeString, out OpcUaNodeId? nodeId) ? nodeId : new OpcUaNodeId(typeString, modelInfo, nsUriToNsInfoMap);
        }

        public static (int NsIndex, int NodeIndex) ParseNodeIdString(string nodeIdString, OpcUaModelInfo? modelInfo, Dictionary<string, OpcUaNamespaceInfo>? nsUriToNsInfoMap)
        {
            // Example: ns=2;i=1234
            // Example: ns=4;s=foo3bar
            // Example: i=85
            string[] parts = nodeIdString.Split(';');
            int nsIndex = 0;
            int nodeIndex = 0;
            foreach (string part in parts)
            {
                if (part.StartsWith("ns="))
                {
                    nsIndex = int.Parse(part.Substring(3));
                }
                else if (part.StartsWith("s="))
                {
                    if (modelInfo == null || nsUriToNsInfoMap == null)
                    {
                        throw new ArgumentNullException(nameof(modelInfo), "modelInfo and nsUriToNsInfoMap cannot be null when parsing string NodeIds with string identifiers.");
                    }

                    if (!nsUriToNsInfoMap.TryGetValue(modelInfo.NamespaceUris[nsIndex], out OpcUaNamespaceInfo? namespaceInfo))
                    {
                        namespaceInfo = new OpcUaNamespaceInfo();
                        nsUriToNsInfoMap[modelInfo.NamespaceUris[nsIndex]] = namespaceInfo;
                    }

                    string nodeString = part.Substring(2);
                    if (!namespaceInfo.NodeStringToNodeIndexMap.TryGetValue(nodeString, out nodeIndex))
                    {
                        nodeIndex = namespaceInfo.NextNodeIndexForNodeString;
                        namespaceInfo.NodeStringToNodeIndexMap[nodeString] = nodeIndex;
                        namespaceInfo.NextNodeIndexForNodeString -= 1;
                    }
                }
                else if (part.StartsWith("i="))
                {
                    nodeIndex = int.Parse(part.Substring(2));
                }
            }

            if (nodeIndex == 0)
            {
                throw new FormatException($"Invalid NodeId string: {nodeIdString}");
            }

            return (nsIndex, nodeIndex);
        }

        public static (int NsIndex, string Name) ParseNameString(string nameString)
        {
            // Example: 2:MyVariable
            // Example: MyObject
            int nsIndex = 0;
            string name = nameString;
            int colonIndex = nameString.IndexOf(':');
            if (colonIndex > 0)
            {
                nsIndex = int.Parse(nameString.Substring(0, colonIndex));
                name = nameString.Substring(colonIndex + 1);
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new FormatException($"Invalid Name string: {nameString}");
            }

            return (nsIndex, name);
        }

        public static List<OpcUaReference> GetReferencesFromXmlNode(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode xmlNode)
        {
            List<OpcUaReference>  references = new List<OpcUaReference>();

            XmlNode? referencesNode = xmlNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "References");
            if (referencesNode != null)
            {
                foreach (XmlNode childNode in referencesNode.ChildNodes)
                {
                    if (childNode.Name == "Reference")
                    {
                        string? referenceTypeString = childNode.Attributes?["ReferenceType"]?.Value;
                        bool isForward = childNode.Attributes?["IsForward"]?.Value != "false";
                        string? targetNodeIdString = childNode.InnerText;

                        if (referenceTypeString != null && targetNodeIdString != null)
                        {
                            OpcUaNodeId referenceTypeNodeId = UaUtil.ParseTypeString(referenceTypeString, modelInfo, nsUriToNsInfoMap);
                            OpcUaNodeId targetNodeId = new OpcUaNodeId(targetNodeIdString, modelInfo, nsUriToNsInfoMap);
                            references.Add(new OpcUaReference(referenceTypeNodeId, targetNodeId, isForward));
                        }
                    }
                }
            }

            return references;
        }

        public static string CleanText(this string text)
        {
            return Regex.Replace(text, "\\s", " ", RegexOptions.Multiline).Replace('\"', '\'').Replace("\\", "\\\\");
        }
    }
}
