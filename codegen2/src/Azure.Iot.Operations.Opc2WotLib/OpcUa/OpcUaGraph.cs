// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class OpcUaGraph
    {
        public const string OpcUaCoreModelUri = "http://opcfoundation.org/UA/";

        public static readonly XmlNamespaceManager NamespaceManager;

        public readonly Dictionary<string, OpcUaModelInfo> ModelUriToModelMap;
        public readonly Dictionary<string, OpcUaNamespaceInfo> NsUriToNsInfoMap;

        static OpcUaGraph()
        {
            XmlNameTable nameTable = new NameTable();
            NamespaceManager = new XmlNamespaceManager(nameTable);
            NamespaceManager.AddNamespace("opc", "http://opcfoundation.org/UA/2011/03/UANodeSet.xsd");
            NamespaceManager.AddNamespace("ua", "http://unifiedautomation.com/Configuration/NodeSet.xsd");
            NamespaceManager.AddNamespace("uax", "http://opcfoundation.org/UA/2008/02/Types.xsd");
        }

        public OpcUaGraph()
        {
            ModelUriToModelMap = new Dictionary<string, OpcUaModelInfo>();
            NsUriToNsInfoMap = new Dictionary<string, OpcUaNamespaceInfo>();
        }

        public IEnumerable<string> GetModelUris()
        {
            return ModelUriToModelMap.Keys.ToList();
        }

        public OpcUaModelInfo GetOpcUaModelInfo(string modelUri)
        {
            if (ModelUriToModelMap.TryGetValue(modelUri, out OpcUaModelInfo? modelInfo))
            {
                return modelInfo;
            }
            else
            {
                throw new ArgumentException($"Model URI '{modelUri}' not found in graph.");
            }
        }

        public string GetThingModel(string modelUri)
        {
            if (ModelUriToModelMap.TryGetValue(modelUri, out OpcUaModelInfo? modelInfo))
            {
                string things = string.Join(",\r\n", modelInfo.NodeIdToObjectTypeMap.Values.Select(t => $"  {{ \"title\": \"{t.BrowseName.Name}\" }}"));
                return $"[\r\n{things}\r\n]";
            }
            else
            {
                throw new ArgumentException($"Model URI '{modelUri}' not found in graph.");
            }
        }

        public void AddNodeset(string xmlText)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlText);
            ArgumentNullException.ThrowIfNull(xmlDoc.DocumentElement, nameof(xmlDoc.DocumentElement));

            XmlNode? modelsNode = xmlDoc.DocumentElement.SelectSingleNode("/opc:UANodeSet/opc:Models", NamespaceManager);
            ArgumentNullException.ThrowIfNull(modelsNode, nameof(modelsNode));

            XmlNode? modelNode = modelsNode.FirstChild;
            ArgumentNullException.ThrowIfNull(modelNode, nameof(modelNode));

            string? modelUri = modelNode.Attributes?["ModelUri"]?.Value;
            ArgumentNullException.ThrowIfNullOrEmpty(modelUri, nameof(modelUri));

            XmlNode? namespaceUrisNode = xmlDoc.DocumentElement.SelectSingleNode("/opc:UANodeSet/opc:NamespaceUris", NamespaceManager);

            XmlNode? aliasesNode = xmlDoc.DocumentElement.SelectSingleNode("/opc:UANodeSet/opc:Aliases", NamespaceManager);
            ArgumentNullException.ThrowIfNull(aliasesNode, nameof(aliasesNode));

            OpcUaModelInfo modelInfo = new OpcUaModelInfo(modelUri, namespaceUrisNode?.ChildNodes, aliasesNode.ChildNodes);
            ModelUriToModelMap[modelUri] = modelInfo;

            Dictionary<string, OpcUaObjectType> effectiveNameToObjectTypeMap = new();

            foreach (XmlNode node in xmlDoc.DocumentElement.ChildNodes)
            {
                OpcUaNode? opcUaNode = null;

                switch (node.Name)
                {
                    case "UADataType":
                        opcUaNode = OpcUaDataType.TryCreate(modelInfo, NsUriToNsInfoMap, node, out OpcUaDataType? dataType) ? dataType : null;
                        break;
                    case "UAObject":
                        opcUaNode = new OpcUaObject(modelInfo, NsUriToNsInfoMap, node);
                        break;
                    case "UAObjectType":
                        OpcUaObjectType opcUaObjectType = new OpcUaObjectType(modelInfo, NsUriToNsInfoMap, node);
                        SetDiscriminator(opcUaObjectType, effectiveNameToObjectTypeMap);
                        modelInfo.NodeIdToObjectTypeMap[opcUaObjectType.NodeId] = opcUaObjectType;
                        opcUaNode = opcUaObjectType;
                        break;
                    case "UAVariable":
                        opcUaNode = new OpcUaVariable(modelInfo, NsUriToNsInfoMap, node);
                        break;
                    case "UAMethod":
                        opcUaNode = new OpcUaMethod(modelInfo, NsUriToNsInfoMap, node);
                        break;
                    case "UAReferenceType":
                        opcUaNode = new OpcUaReferenceType(modelInfo, NsUriToNsInfoMap, node);
                        break;
                }

                if (opcUaNode != null)
                {
                    string nodeNsUri = opcUaNode.NodeIdNamespace;
                    if (!NsUriToNsInfoMap.TryGetValue(nodeNsUri, out OpcUaNamespaceInfo? nsInfo))
                    {
                        nsInfo = new OpcUaNamespaceInfo();
                        NsUriToNsInfoMap[nodeNsUri] = nsInfo;
                    }
                    nsInfo.NodeIndexToNodeMap[opcUaNode.NodeId.NodeIndex] = opcUaNode;
                }
            }
        }

        private void SetDiscriminator(OpcUaObjectType newObjectType, Dictionary<string, OpcUaObjectType> effectiveNameToObjectTypeMap)
        {
            if (effectiveNameToObjectTypeMap.TryGetValue(newObjectType.EffectiveName, out OpcUaObjectType? extantObjectType))
            {
                if (extantObjectType.Discriminator == 0)
                {
                    extantObjectType.Discriminator = 1;
                }

                newObjectType.Discriminator = extantObjectType.Discriminator + 1;
            }

            effectiveNameToObjectTypeMap[newObjectType.EffectiveName] = newObjectType;
        }
    }
}
