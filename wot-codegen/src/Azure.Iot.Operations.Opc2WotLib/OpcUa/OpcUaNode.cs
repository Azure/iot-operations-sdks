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
        private Dictionary<string, UaVariableRecord>? variableRecords = null;

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
            IsDeprecated = xmlNode.Attributes?["ReleaseStatus"]?.Value == "Deprecated";
            Description = xmlNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(node => node.Name == "Description")?.InnerText.CleanText();
            References = new List<OpcUaReference>();
            NsUriToNsInfoMap = nsUriToNsInfoMap;
            Discriminator = 0;
        }

        public OpcUaModelInfo DefiningModel { get; }

        public OpcUaNodeId NodeId { get; }

        public OpcUaName BrowseName { get; }

        public string? SymbolicName { get; }

        public bool IsDeprecated { get; }

        public string? Description { get; }

        public List<OpcUaReference> References { get; protected set; }

        public int Discriminator { get; set; }

        public string EffectiveName { get => SymbolicName ?? BrowseName.Name; }

        public string DiscriminatedEffectiveName => Discriminator == 0 ? EffectiveName : $"{EffectiveName}_{Discriminator}";

        public string NodeIdNamespace { get => DefiningModel.NamespaceUris[NodeId.NsIndex]; }

        public string? BrowseNamespace { get => BrowseName.NsIndex == 0 ? null : DefiningModel.NamespaceUris[BrowseName.NsIndex]; }

        public IEnumerable<OpcUaNode> Properties
        {
            get => References
                .Where(r => r.IsForward && r.ReferenceType.IsPropertyReference)
                .Select(r => GetReferencedOpcUaNode(r.Target))
                .Where(n => !n.IsDeprecated);
        }

        public IEnumerable<OpcUaNode> Components
        {
            get => References
                .Where(r => r.IsForward && r.ReferenceType.IsComponentReference)
                .Select(r => GetReferencedOpcUaNode(r.Target))
                .Where(n => !n.IsDeprecated);
        }

        public List<OpcUaMethod> Methods { get => Components.OfType<OpcUaMethod>().ToList(); }

        public Dictionary<string, UaVariableRecord> VariableRecords
        {
            get
            {
                if (variableRecords == null)
                {
                    variableRecords = new Dictionary<string, UaVariableRecord>();

                    foreach (OpcUaVariable uaVariable in Components.OfType<OpcUaVariable>())
                    {
                        uaVariable.CollectVariableRecords(variableRecords, true);
                    }

                    foreach (OpcUaVariable uaVariable in Properties.OfType<OpcUaVariable>())
                    {
                        uaVariable.CollectVariableRecords(variableRecords, false);
                    }
                }

                return variableRecords;
            }
        }

        public OpcUaNode GetReferencedOpcUaNode(OpcUaNodeId nodeId) =>
            NsUriToNsInfoMap[this.DefiningModel.NamespaceUris[nodeId.NsIndex]].NodeIndexToNodeMap[nodeId.NodeIndex];

        public string GetTypeRef()
        {
            if (this.BrowseNamespace == null)
            {
                return $"org.opcfoundation.UA.{this.EffectiveName}";
            }

            Uri uri = new Uri(this.BrowseNamespace);
            string reversedHost = string.Join('.', uri.Host.Split('.').Reverse());
            string path = uri.AbsolutePath.Trim('/').Replace('/', '.');
            string prefix = string.IsNullOrEmpty(path) ? reversedHost : $"{reversedHost}.{path}";
            return $"{prefix}.{this.EffectiveName}";
        }

        protected Dictionary<string, OpcUaNamespaceInfo> NsUriToNsInfoMap { get; }
    }
}
