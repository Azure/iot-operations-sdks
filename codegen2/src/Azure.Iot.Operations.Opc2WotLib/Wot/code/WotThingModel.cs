// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public partial class WotThingModel : ITemplateTransform
    {
        private string specName;
        private string thingName;
        private string typeRef;
        private bool isEvent;
        private bool isComposite;
        private List<string> baseModelRefs;
        private List<LinkInfo> linkInfos;
        private List<WotAction> actions;
        private List<WotProperty> properties;
        private List<WotEvent> events;

        public WotThingModel(string specName, OpcUaObjectType uaObjectType, LinkRelRuleEngine linkRelRuleEngine)
        {
            this.specName = specName;
            this.thingName = WotUtil.LegalizeName(uaObjectType.DiscriminatedEffectiveName, specName);
            this.typeRef = $"nsu={uaObjectType.NodeIdNamespace};i={uaObjectType.NodeId.NodeIndex}";

            bool isTypeDefinition = uaObjectType.DefiningModel.TypeDefinitionNodeIds.Contains(uaObjectType.NodeId);
            this.isEvent = uaObjectType.AncestorNames.Contains("OpcUaCore_BaseEventType");
            this.isComposite = !isTypeDefinition && !this.isEvent && !uaObjectType.IsAbstract;

            this.baseModelRefs = uaObjectType.BaseModels
                .Select(node => GetModelRef(uaObjectType, node)).ToList();
            this.linkInfos = uaObjectType.TypeAndObjectOfReferences
                .Where(t => t.Item1.NsIndex != 0 || t.Item1.IsComponentReference)
                .Select(t => GetLinkInfo(uaObjectType, t.Item1, t.Item2, linkRelRuleEngine))
                .ToList();

            this.actions = uaObjectType.Methods.OrderBy(m => m.EffectiveName).Select(m => new WotAction(specName, this.thingName, m)).ToList();
            this.properties = uaObjectType.VariableRecords.OrderBy(r => r.Key).Select(r => new WotProperty(specName, this.thingName, r.Value.UaVariable, r.Key, r.Value.ContainedIn, r.Value.Contains)).ToList();
            this.events = uaObjectType.VariableRecords.OrderBy(r => r.Key).Where(r => r.Value.IsDataVariable).Select(r => new WotEvent(specName, this.thingName, r.Value.UaVariable, r.Key, r.Value.ContainedIn, r.Value.Contains)).ToList();
        }

        private string GetModelRef(OpcUaObjectType sourceObjectType, OpcUaObjectType targetObjectType)
        {
            string targetSpecName = SpecMapper.GetSpecNameFromUri(targetObjectType.DefiningModel.ModelUri);

            string fileRef = ReferenceEquals(sourceObjectType.DefiningModel, targetObjectType.DefiningModel) ? string.Empty : $"./{targetSpecName}.TM.json";
            return $"{fileRef}#title={WotUtil.LegalizeName(targetObjectType.DiscriminatedEffectiveName, targetSpecName)}";
        }

        private LinkInfo GetLinkInfo(OpcUaObjectType sourceObjectType, OpcUaNodeId referenceTypeNodeId, OpcUaObject targetObject, LinkRelRuleEngine linkRelRuleEngine)
        {
            OpcUaObjectType targetObjectType = (OpcUaObjectType)targetObject.GetReferencedOpcUaNode(targetObject.HasTypeDefinitionNodeId!);
            string targetModelRef = GetModelRef(sourceObjectType, targetObjectType);

            if (referenceTypeNodeId.NsIndex != 0)
            {
                return new LinkInfo(targetModelRef, "aov:typedReference", targetObject.EffectiveName, sourceObjectType.GetReferencedOpcUaNode(referenceTypeNodeId).EffectiveName);
            }
            else
            {
                return new LinkInfo(targetModelRef, linkRelRuleEngine.GetLinkRel(sourceObjectType, targetObjectType, targetObject.HasModellingRule), targetObject.EffectiveName, null);
            }
        }

        private record LinkInfo(string Href, string Rel, string RefName, string? RefType);
    }
}
