// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class OpcUaObjectType : OpcUaNode
    {
        private HashSet<string>? ancestorNames = null;
        private Dictionary<string, UaVariableRecord>? variableRecords = null;

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

        public IEnumerable<OpcUaObjectType> BaseModels
        {
            get => References
                .Where(r => !r.IsForward && r.ReferenceType.IsSubtypeReference)
                .Select(r => GetReferencedOpcUaNode(r.Target))
                .Cast<OpcUaObjectType>();
        }

        public IEnumerable<(OpcUaNodeId, OpcUaObject)> TypeAndObjectOfReferences
        {
            get => References
                .Where(r => r.IsForward && (r.ReferenceType.NsIndex != 0 || r.ReferenceType.IsComponentReference))
                .Select(r => (r.ReferenceType, GetReferencedOpcUaNode(r.Target)))
                .Where(t => t.Item2 is OpcUaObject)
                .Select(t => (t.Item1, (OpcUaObject)t.Item2))
                .Where(o => o.Item2.HasTypeDefinitionNodeId != null);
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
