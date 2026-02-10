// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class OpcUaNode
    {
        public OpcUaNode(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode xmlNode)
        {
            string? nodeIdString = xmlNode.Attributes?["NodeId"]?.Value;
            ArgumentNullException.ThrowIfNullOrEmpty(nodeIdString, nameof(nodeIdString));

            string? browseNameString = xmlNode.Attributes?["BrowseName"]?.Value;
            ArgumentNullException.ThrowIfNullOrEmpty(browseNameString, nameof(browseNameString));

            DefiningModel = modelInfo;
            NodeId = new OpcUaNodeId(nodeIdString, modelInfo, nsUriToNsInfoMap);
            BrowseName = new OpcUaName(browseNameString);
            SymbolicName = xmlNode.Attributes?["SymbolicName"]?.Value;
            Description = xmlNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "Description")?.InnerText.CleanText();
            References = new List<OpcUaReference>();
        }

        public OpcUaModelInfo DefiningModel { get; }

        public OpcUaNodeId NodeId { get; }

        public OpcUaName BrowseName { get; }

        public string? SymbolicName { get; }

        public string? Description { get; }

        public List<OpcUaReference> References { get; protected set; }

        public string EffectiveName { get => SymbolicName ?? BrowseName.Name; }

        public string NodeIdNamespace { get => DefiningModel.NamespaceUris[NodeId.NsIndex]; }

        public string? BrowseNamespace { get => BrowseName.NsIndex == 0 ? null : DefiningModel.NamespaceUris[BrowseName.NsIndex]; }

        public OpcUaNode GetReferencedOpcUaNode(OpcUaNodeId nodeId, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap) =>
            nsUriToNsInfoMap[this.DefiningModel.NamespaceUris[nodeId.NsIndex]].NodeIndexToNodeMap[nodeId.NodeIndex];

        public IEnumerable<OpcUaNode> GetProperties(Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap)
        {
            return References
                .Where(r => r.IsForward && r.ReferenceType.IsPropertyReference)
                .Select(r => GetReferencedOpcUaNode(r.Target, nsUriToNsInfoMap));
        }

        public IEnumerable<OpcUaNode> GetComponents(Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap)
        {
            return References
                .Where(r => r.IsForward && r.ReferenceType.IsComponentReference)
                .Select(r => GetReferencedOpcUaNode(r.Target, nsUriToNsInfoMap));
        }
    }
}
