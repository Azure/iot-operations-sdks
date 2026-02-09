// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class OpcUaObjectType : OpcUaNode
    {
        public OpcUaObjectType(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode objectTypeNode)
            : base(modelInfo, nsUriToNsInfoMap, objectTypeNode)
        {
            IsAbstract = objectTypeNode.Attributes?["IsAbstract"]?.Value == "true";
            References = UaUtil.GetReferencesFromXmlNode(modelInfo, nsUriToNsInfoMap, objectTypeNode);
            Discriminator = 0;
        }

        public bool IsAbstract { get; }

        public int Discriminator { get; set; }

        public string DiscriminatedEffectiveName => Discriminator == 0 ? EffectiveName : $"{EffectiveName}_{Discriminator}";

        public IEnumerable<OpcUaObjectType> GetBaseModels(Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap)
        {
            return References
                .Where(r => !r.IsForward && r.ReferenceType.IsSubtypeReference)
                .Select(r => GetReferencedOpcUaNode(r.Target, nsUriToNsInfoMap))
                .Cast<OpcUaObjectType>();
        }

        public IEnumerable<(OpcUaNodeId, OpcUaObject)> GetTypeAndObjectOfReferences(Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap)
        {
            return References
                .Where(r => r.IsForward && (r.ReferenceType.NsIndex != 0 || r.ReferenceType.IsComponentReference))
                .Select(r => (r.ReferenceType, GetReferencedOpcUaNode(r.Target, nsUriToNsInfoMap)))
                .Where(t => t.Item2 is OpcUaObject)
                .Select(t => (t.Item1, (OpcUaObject)t.Item2))
                .Where(o => o.Item2.HasTypeDefinitionNodeId != null);
        }
    }
}
