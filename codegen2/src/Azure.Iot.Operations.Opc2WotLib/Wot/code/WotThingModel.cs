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
        private List<string> baseModelRefs;
        private List<(string, string, string?)> hrefRelReftypes;
        private List<WotAction> actions;
        private List<WotProperty> properties;
        private List<WotEvent> events;

        public WotThingModel(string specName, OpcUaObjectType uaObjectType, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap)
        {
            this.specName = specName;
            this.thingName = WotUtil.LegalizeName(uaObjectType.DiscriminatedEffectiveName, specName);
            this.baseModelRefs = uaObjectType.GetBaseModels(nsUriToNsInfoMap)
                .Select(node => GetModelRef(uaObjectType, node)).ToList();
            this.hrefRelReftypes = uaObjectType.GetTypeAndObjectOfReferences(nsUriToNsInfoMap)
                .Where(t => t.Item1.NsIndex != 0 || t.Item1.IsComponentReference)
                .Select(t => GetHrefRelReftype(uaObjectType, t.Item1, t.Item2, nsUriToNsInfoMap))
                .ToList();

            List<OpcUaMethod> actionVariables = uaObjectType.GetComponents(nsUriToNsInfoMap).OfType<OpcUaMethod>().ToList();
            List<OpcUaVariable> propertyVariables = uaObjectType.GetPropertiesAndComponents(nsUriToNsInfoMap).OfType<OpcUaVariable>().ToList();
            List<OpcUaVariable> eventVariables = uaObjectType.GetComponents(nsUriToNsInfoMap).OfType<OpcUaVariable>().ToList();

            this.actions = actionVariables.Select(m => new WotAction(specName, this.thingName, m, nsUriToNsInfoMap)).ToList();
            this.properties = propertyVariables.Select(p => new WotProperty(specName, this.thingName, p, nsUriToNsInfoMap)).ToList();
            this.events = eventVariables.Select(e => new WotEvent(specName, this.thingName, e, nsUriToNsInfoMap)).ToList();
        }

        private string GetModelRef(OpcUaObjectType sourceObjectType, OpcUaObjectType targetObjectType)
        {
            string targetSpecName = SpecMapper.GetSpecNameFromUri(targetObjectType.DefiningModel.ModelUri);

            string fileRef = ReferenceEquals(sourceObjectType.DefiningModel, targetObjectType.DefiningModel) ? string.Empty : $"./{targetSpecName}.TM.json";
            return $"{fileRef}#title={WotUtil.LegalizeName(targetObjectType.DiscriminatedEffectiveName, targetSpecName)}";
        }

        private (string, string, string?) GetHrefRelReftype(OpcUaObjectType sourceObjectType, OpcUaNodeId referenceTypeNodeId, OpcUaObject targetObject, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap)
        {
            string targetModelRef = GetModelRef(sourceObjectType, (OpcUaObjectType)targetObject.GetReferencedOpcUaNode(targetObject.HasTypeDefinitionNodeId!, nsUriToNsInfoMap));

            if (referenceTypeNodeId.NsIndex != 0)
            {
                return (targetModelRef, "aov:typedReference", sourceObjectType.GetReferencedOpcUaNode(referenceTypeNodeId, nsUriToNsInfoMap).EffectiveName);
            }
            else
            {
                return (targetModelRef, "aov:reference", null);
            }
        }
    }
}
