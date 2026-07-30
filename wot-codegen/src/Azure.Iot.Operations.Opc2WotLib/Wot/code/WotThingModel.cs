// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public partial class WotThingModel : ITemplateTransform
    {
        private static readonly OpcUaNodeId StringDataTypeNodeId = new OpcUaNodeId(0, 12);

        private string specName;
        private string thingName;
        private string typeRef;
        private bool isIntegrated;
        private bool inheritVars;
        private bool isEvent;
        private bool isComposite;
        private HashSet<string> baseModelRefs;
        private List<LinkInfo> linkInfos;
        private Dictionary<string, WotDataSchema> schemaDefinitions;
        private List<WotAction> actions;
        private List<WotProperty> properties;
        private List<WotEvent> events;
        private List<string> optionalActionNames;
        private List<string> optionalPropertyNames;
        private List<string> optionalEventNames;
        private bool areUnitsInUse;

        public WotThingModel(string specName, OpcUaObjectType uaObjectType, LinkRelRuleEngine linkRelRuleEngine, bool isIntegrated, bool inheritVars)
        {
            this.specName = specName;
            this.thingName = WotUtil.LegalizeName(uaObjectType.DiscriminatedEffectiveName, specName);
            this.typeRef = uaObjectType.GetTypeRef();
            this.isIntegrated = isIntegrated;
            this.inheritVars = inheritVars;

            bool isTypeDefinition = uaObjectType.DefiningModel.TypeDefinitionNodeIds.Contains(uaObjectType.NodeId);
            this.isEvent = uaObjectType.AncestorNames.Contains("OpcUaCore_BaseEventType");
            this.isComposite = !isTypeDefinition && !this.isEvent && !uaObjectType.IsAbstract;

            this.baseModelRefs = uaObjectType.BaseModels
                .Select(node => GetModelRef(uaObjectType, node))
                .ToHashSet();
            this.linkInfos = uaObjectType.TypeAndObjectOfReferences
                .Where(t => t.Item1.NsIndex != 0 || t.Item1.IsComponentReference || t.Item1.IsAddInReference)
                .Select(t => GetLinkInfo(uaObjectType, t.Item1, t.Item2, linkRelRuleEngine))
                .ToList();

            this.schemaDefinitions = new Dictionary<string, WotDataSchema>(StringComparer.Ordinal);
            foreach (OpcUaDataTypeEnum dataTypeEnum in uaObjectType.ExtractEnums().OrderBy(dt => dt.EffectiveName, StringComparer.Ordinal))
            {
                this.schemaDefinitions.Add(dataTypeEnum.EffectiveName, new WotDataSchemaEnum(dataTypeEnum));
            }

            List<OpcUaVariableType> variableTypes = uaObjectType.VariableRecords.Values
                .Select(r => r.UaVariable.CustomVariableType)
                .Where(vt => vt != null)
                .Cast<OpcUaVariableType>()
                .Distinct()
                .ToList();
            Dictionary<OpcUaVariableType, string> variableTypeSchemaNames = WotVariableTypeSchema.GetSchemaNames(variableTypes, this.schemaDefinitions.Keys);
            foreach (KeyValuePair<OpcUaVariableType, string> variableTypeSchemaName in variableTypeSchemaNames)
            {
                if (!this.schemaDefinitions.TryAdd(variableTypeSchemaName.Value, WotVariableTypeSchema.Create(variableTypeSchemaName.Key)))
                {
                    throw new InvalidOperationException($"Schema name '{variableTypeSchemaName.Value}' is used by both a DataType and a VariableType in Thing Model '{this.thingName}'.");
                }
            }

            this.actions = uaObjectType.Methods.OrderBy(m => m.EffectiveName).Select(m => new WotAction(specName, this.thingName, m, false)).ToList();
            this.properties = uaObjectType.VariableRecords
                .OrderBy(r => r.Key)
                .Select(r => new WotProperty(specName, this.thingName, r.Value.UaVariable, r.Key, r.Value.ContainedIn, r.Value.Contains, false, GetVariableTypeSchemaName(r.Value.UaVariable, variableTypeSchemaNames)))
                .ToList();
            this.events = uaObjectType.VariableRecords
                .OrderBy(r => r.Key)
                .Where(r => r.Value.IsDataVariable)
                .Select(r => new WotEvent(specName, this.thingName, r.Value.UaVariable, r.Key, r.Value.ContainedIn, r.Value.Contains, false, GetVariableTypeSchemaName(r.Value.UaVariable, variableTypeSchemaNames)))
                .ToList();

            List<string> unitfulPropertyNames = this.properties.Where(p => p.UsesUnits).Select(p => p.PropertyName).ToList();
            List<string> unitfulEventNames = this.events.Where(e => e.UsesUnits).Select(e => e.EventName).ToList();
            foreach (string unitfulPropertyName in unitfulPropertyNames)
            {
                string whichAffordances = unitfulEventNames.Contains(unitfulPropertyName) ? "property and event" : "property";
                string description = $"Unit designator for {whichAffordances} with name {unitfulPropertyName}, expressed as a 2- or 3-character UN/ECE code";
                this.properties.Add(new WotProperty(specName, this.thingName, $"{unitfulPropertyName}_EngineeringUnits", StringDataTypeNodeId, true, false, description, false));
            }

            this.optionalActionNames = this.actions.Where(a => !a.IsMandatory).Select(a => a.ActionName).ToList();
            this.optionalPropertyNames = this.properties.Where(p => !p.IsMandatory).Select(p => p.PropertyName).ToList();
            this.optionalEventNames = this.events.Where(e => !e.IsMandatory).Select(e => e.EventName).ToList();

            this.areUnitsInUse = unitfulPropertyNames.Any();
        }

        private static string? GetVariableTypeSchemaName(OpcUaVariable variable, Dictionary<OpcUaVariableType, string> schemaNames)
        {
            return variable.CanUseVariableTypeSchemaReference &&
                variable.CustomVariableType != null &&
                schemaNames.TryGetValue(variable.CustomVariableType, out string? schemaName)
                ? schemaName
                : null;
        }

        private string GetModelRef(OpcUaObjectType sourceObjectType, OpcUaObjectType targetObjectType)
        {
            string targetSpecName = SpecMapper.GetSpecNameFromUri(targetObjectType.DefiningModel.ModelUri);

            string fileRef = isIntegrated || ReferenceEquals(sourceObjectType.DefiningModel, targetObjectType.DefiningModel) ? string.Empty : $"./{targetSpecName}.TM.json";
            return $"{fileRef}#title={WotUtil.LegalizeName(targetObjectType.DiscriminatedEffectiveName, targetSpecName)}";
        }

        private LinkInfo GetLinkInfo(OpcUaObjectType sourceObjectType, OpcUaNodeId referenceTypeNodeId, OpcUaObject targetObject, LinkRelRuleEngine linkRelRuleEngine)
        {
            OpcUaObjectType targetObjectType = (OpcUaObjectType)targetObject.GetReferencedOpcUaNode(targetObject.HasTypeDefinitionNodeId!);
            string targetModelRef = GetModelRef(sourceObjectType, targetObjectType);

            if (referenceTypeNodeId.NsIndex != 0)
            {
                return new LinkInfo(targetModelRef, "dov:typedReference", targetObject.EffectiveName, sourceObjectType.GetReferencedOpcUaNode(referenceTypeNodeId).EffectiveName);
            }
            else
            {
                return new LinkInfo(targetModelRef, linkRelRuleEngine.GetLinkRel(sourceObjectType, targetObjectType, targetObject.HasModellingRule), targetObject.EffectiveName, null);
            }
        }

        private record LinkInfo(string Href, string Rel, string RefName, string? RefType);
    }
}
