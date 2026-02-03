// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Xml;

    public class OpcUaModelInfo
    {
        // Aliases used in several companion specs without being defined therein.
        // The values come from other companion specs that do define them.
        private static readonly Dictionary<string, OpcUaNodeId> HardCodedAliases = new()
        {
            { "IdType", new OpcUaNodeId(0, 256) },
            { "NumericRange", new OpcUaNodeId(0, 291) },
        };

        public OpcUaModelInfo(string modelUri, XmlNodeList? namespaceUriNodes, XmlNodeList aliasNodes)
        {
            ModelUri = modelUri;

            NamespaceUris = new string[(namespaceUriNodes?.Count ?? 0) + 1];
            int nsIx = 0;
            NamespaceUris[nsIx++] = OpcUaGraph.OpcUaCoreModelUri;
            if (namespaceUriNodes != null)
            {
                foreach (XmlNode node in namespaceUriNodes)
                {
                    NamespaceUris[nsIx++] = node.InnerText;
                }
            }

            AliasMap = new Dictionary<string, OpcUaNodeId>(HardCodedAliases);
            foreach (XmlNode node in aliasNodes)
            {
                string? alias = node.Attributes?["Alias"]?.Value;
                string? nodeId = node.InnerText;

                ArgumentNullException.ThrowIfNullOrEmpty(alias, nameof(alias));
                ArgumentNullException.ThrowIfNullOrEmpty(nodeId, nameof(nodeId));

                AliasMap[alias] = new OpcUaNodeId(nodeId, null, null);
            }

            NodeIdToObjectTypeMap = new Dictionary<OpcUaNodeId, OpcUaObjectType>();
        }

        public string ModelUri { get; }

        public string[] NamespaceUris { get; }

        public Dictionary<string, OpcUaNodeId> AliasMap { get; }

        public Dictionary<OpcUaNodeId, OpcUaObjectType> NodeIdToObjectTypeMap { get; }
    }
}
