// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class OpcUaObjectType : OpcUaNode
    {
        private List<OpcUaObjectType>? baseModels = null;
        private List<(OpcUaNodeId, OpcUaObject)>? typeAndObjectOfReferences = null;
        private HashSet<string>? ancestorNames = null;

        public OpcUaObjectType(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode objectTypeNode)
            : base(modelInfo, nsUriToNsInfoMap, objectTypeNode)
        {
            IsAbstract = objectTypeNode.Attributes?["IsAbstract"]?.Value == "true";
            References = UaUtil.GetReferencesFromXmlNode(modelInfo, nsUriToNsInfoMap, objectTypeNode);
        }

        public bool IsAbstract { get; }

        public List<OpcUaObjectType> BaseModels
        {
            get
            {
                if (baseModels == null)
                {
                    baseModels = References
                        .Where(r => !r.IsForward && r.ReferenceType.IsSubtypeReference)
                        .Select(r => GetReferencedOpcUaNode(r.Target))
                        .Cast<OpcUaObjectType>()
                        .ToList();
                }

                return baseModels;
            }
        }

        public List<(OpcUaNodeId, OpcUaObject)> TypeAndObjectOfReferences
        {
            get
            {
                if (typeAndObjectOfReferences == null)
                {
                    typeAndObjectOfReferences = References
                        .Where(r => r.IsForward && (r.ReferenceType.NsIndex != 0 || r.ReferenceType.IsComponentReference))
                        .Select(r => (r.ReferenceType, GetReferencedOpcUaNode(r.Target)))
                        .Where(t => t.Item2 is OpcUaObject)
                        .Select(t => (t.Item1, (OpcUaObject)t.Item2))
                        .Where(o => o.Item2.HasTypeDefinitionNodeId != null)
                        .ToList();
                }

                return typeAndObjectOfReferences;
            }
        }

        public HashSet<string> AncestorNames
        {
            get
            {
                if (ancestorNames == null)
                {
                    this.ancestorNames = new HashSet<string>();
                    ComputeAncestorNames(this.ancestorNames);
                }

                return ancestorNames;
            }
        }

        private void ComputeAncestorNames(HashSet<string> ancestorNames)
        {
            foreach (OpcUaObjectType baseModel in BaseModels)
            {
                if (ancestorNames.Add(WotUtil.LegalizeName(baseModel.DiscriminatedEffectiveName, SpecMapper.GetSpecNameFromUri(baseModel.DefiningModel.ModelUri))))
                {
                    baseModel.ComputeAncestorNames(ancestorNames);
                }
            }
        }
    }
}
