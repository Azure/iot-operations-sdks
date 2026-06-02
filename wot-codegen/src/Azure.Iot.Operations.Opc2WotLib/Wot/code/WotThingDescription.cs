// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public partial class WotThingDescription : ITemplateTransform
    {
        private static readonly OpcUaNodeId StringDataTypeNodeId = new OpcUaNodeId(0, 12);

        private string specName;
        private string thingName;
        private string typeRef;
        private string? definingModelRef;
        private List<WotAction> actions;
        private List<WotProperty> properties;
        private List<WotEvent> events;
        private bool areUnitsInUse;

        public WotThingDescription(string specName, OpcUaObject uaObject)
        {
            this.specName = specName;
            this.thingName = WotUtil.LegalizeName(uaObject.DiscriminatedEffectiveName, specName);
            this.typeRef = uaObject.GetTypeRef();
            this.definingModelRef = uaObject.HasTypeDefinition != null && uaObject.HasTypeDefinition.NodeId.NsIndex != 0 ? GetModelRef(uaObject, uaObject.HasTypeDefinition) : null;

            this.actions = uaObject.Methods.OrderBy(m => m.EffectiveName).Select(m => new WotAction(specName, this.thingName, m)).ToList();
            this.properties = uaObject.VariableRecords.OrderBy(r => r.Key).Select(r => new WotProperty(specName, this.thingName, r.Value.UaVariable, r.Key, r.Value.ContainedIn, r.Value.Contains)).ToList();
            this.events = uaObject.VariableRecords.OrderBy(r => r.Key).Where(r => r.Value.IsDataVariable).Select(r => new WotEvent(specName, this.thingName, r.Value.UaVariable, r.Key, r.Value.ContainedIn, r.Value.Contains)).ToList();

            List<string> unitfulPropertyNames = this.properties.Where(p => p.UsesUnits).Select(p => p.PropertyName).ToList();
            List<string> unitfulEventNames = this.events.Where(e => e.UsesUnits).Select(e => e.EventName).ToList();
            foreach (string unitfulPropertyName in unitfulPropertyNames)
            {
                string whichAffordances = unitfulEventNames.Contains(unitfulPropertyName) ? "property and event" : "property";
                string description = $"Unit designator for {whichAffordances} with name {unitfulPropertyName}, expressed as a 2- or 3-character UN/ECE code";
                this.properties.Add(new WotProperty(specName, this.thingName, $"{unitfulPropertyName}_EngineeringUnits", StringDataTypeNodeId, true, false, description));
            }

            this.areUnitsInUse = unitfulPropertyNames.Any();
        }

        private string GetModelRef(OpcUaObject sourceObject, OpcUaObjectType targetObjectType)
        {
            string targetSpecName = SpecMapper.GetSpecNameFromUri(targetObjectType.DefiningModel.ModelUri);
            return $"#title={WotUtil.LegalizeName(targetObjectType.DiscriminatedEffectiveName, targetSpecName)}";
        }
    }
}
