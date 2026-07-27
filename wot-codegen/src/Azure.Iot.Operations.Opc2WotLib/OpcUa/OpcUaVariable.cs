// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Xml;

    public class OpcUaVariable : OpcUaNode
    {
        private OpcUaVariableType? hasTypeDefinition;

        public OpcUaVariable(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode variableNode)
            : base(modelInfo, nsUriToNsInfoMap, variableNode)
        {
            string? dataTypeString = variableNode.Attributes?["DataType"]?.Value;
            if (!string.IsNullOrEmpty(dataTypeString))
            {
                DataType = UaUtil.ParseTypeString(dataTypeString, modelInfo, nsUriToNsInfoMap);
            }

            string? valueRankString = variableNode.Attributes?["ValueRank"]?.Value;
            DeclaredValueRank = valueRankString != null ? int.Parse(valueRankString) : null;
            AccessLevel = int.Parse(variableNode.Attributes?["AccessLevel"]?.Value ?? "0");
            References = UaUtil.GetReferencesFromXmlNode(modelInfo, nsUriToNsInfoMap, variableNode);
            HasTypeDefinitionNodeId = References
                .FirstOrDefault(r => r.IsForward && r.ReferenceType.IsTypeDefinitionReference)
                ?.Target;
            Arguments = GetArgumentsFromXmlNode(modelInfo, nsUriToNsInfoMap, variableNode);
            UnitId = dataTypeString == "EUInformation" ? GetUnitIdFromXmlNode(variableNode) : 0;

            IsPlaceholder = References.Any(r => 
                r.IsForward &&
                r.ReferenceType.IsModellingRuleReference &&
                r.Target.IsRulePlaceholder);

            IsMandatory = References.Any(r =>
                r.IsForward &&
                r.ReferenceType.IsModellingRuleReference &&
                r.Target.IsRuleMandatory);
        }

        public OpcUaNodeId? DataType { get; }

        public int? DeclaredValueRank { get; }

        public int ValueRank => int.Max(DeclaredValueRank ?? 0, 0);

        public OpcUaNodeId? EffectiveDataType => DataType ?? HasTypeDefinition?.EffectiveDataType;

        public OpcUaNode EffectiveDataTypeSource => DataType != null ? this : (OpcUaNode?)HasTypeDefinition?.EffectiveDataTypeSource ?? this;

        public int EffectiveValueRank => DeclaredValueRank != null ? int.Max(DeclaredValueRank.Value, 0) : HasTypeDefinition?.EffectiveValueRank ?? 0;

        public OpcUaNodeId? HasTypeDefinitionNodeId { get; }

        public OpcUaVariableType? HasTypeDefinition
        {
            get
            {
                if (hasTypeDefinition == null && HasTypeDefinitionNodeId != null)
                {
                    string namespaceUri = DefiningModel.NamespaceUris[HasTypeDefinitionNodeId.NsIndex];
                    if (NsUriToNsInfoMap.TryGetValue(namespaceUri, out OpcUaNamespaceInfo? namespaceInfo) &&
                        namespaceInfo.NodeIndexToNodeMap.TryGetValue(HasTypeDefinitionNodeId.NodeIndex, out OpcUaNode? node))
                    {
                        hasTypeDefinition = node as OpcUaVariableType;
                    }
                }

                return hasTypeDefinition;
            }
        }

        public OpcUaVariableType? CustomVariableType
        {
            get
            {
                if (HasTypeDefinitionNodeId == null ||
                    DefiningModel.NamespaceUris[HasTypeDefinitionNodeId.NsIndex] == OpcUaGraph.OpcUaCoreModelUri)
                {
                    return null;
                }

                return HasTypeDefinition;
            }
        }

        public bool CanUseVariableTypeSchemaReference
        {
            get
            {
                OpcUaVariableType? variableType = CustomVariableType;
                if (variableType == null)
                {
                    return false;
                }

                if (DataType != null &&
                    !AreSameNodeIds(DataType, this, variableType.EffectiveDataType, variableType.EffectiveDataTypeSource))
                {
                    return false;
                }

                return DeclaredValueRank == null || int.Max(DeclaredValueRank.Value, 0) == variableType.EffectiveValueRank;
            }
        }

        public int AccessLevel { get; }

        public Dictionary<string, OpcUaObjectField> Arguments { get; }

        public int UnitId { get; }

        public bool IsPlaceholder { get; }

        public bool IsMandatory { get; }

        public bool TryGetEngineeringUnits([NotNullWhen(true)] out OpcUaVariable? engUnitsVariable)
        {
            engUnitsVariable = Properties.OfType<OpcUaVariable>().FirstOrDefault(v => v.BrowseName.Name == "EngineeringUnits");
            return engUnitsVariable != null;
        }

        public void CollectVariableRecords(Dictionary<string, UaVariableRecord> variableRecords, bool isDataVariable, string? parentContainer = null, List<string>? parentContents = null)
        {
            string variableName = parentContainer != null ? $"{parentContainer}_{this.EffectiveName}" : this.EffectiveName;

            List<string> myContents = new();
            variableRecords[variableName] = new UaVariableRecord(this, parentContainer, myContents, isDataVariable);
            if (parentContents != null)
            {
                parentContents.Add(variableName);
            }

            if (isDataVariable)
            {
                foreach (OpcUaVariable uaVariable in Components.OfType<OpcUaVariable>())
                {
                    uaVariable.CollectVariableRecords(variableRecords, true, variableName, myContents);
                }
            }
        }

        private Dictionary<string, OpcUaObjectField> GetArgumentsFromXmlNode(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, XmlNode xmlNode)
        {
            Dictionary<string, OpcUaObjectField> arguments = new Dictionary<string, OpcUaObjectField>();

            XmlNodeList? argNodeList = xmlNode.SelectNodes("descendant::uax:Argument", OpcUaGraph.NamespaceManager);
            if (argNodeList == null)
            {
                return arguments;
            }

            foreach (XmlNode argNode in argNodeList)
            {
                string? argName = argNode.SelectSingleNode("child::uax:Name", OpcUaGraph.NamespaceManager)?.InnerText;
                if (argName != null)
                {
                    string? dataTypeString = argNode.SelectSingleNode("child::uax:DataType/child::uax:Identifier", OpcUaGraph.NamespaceManager)?.InnerText;
                    OpcUaNodeId? dataType = dataTypeString != null ? UaUtil.ParseTypeString(dataTypeString, modelInfo, nsUriToNsInfoMap) : null;
                    int valueRank = int.Parse(argNode.SelectSingleNode("child::uax:ValueRank", OpcUaGraph.NamespaceManager)?.InnerText ?? "0");
                    string? description = argNode.SelectSingleNode("child::uax:Description/child::uax:Text", OpcUaGraph.NamespaceManager)?.InnerText.CleanText();
                    arguments[argName] = new OpcUaObjectField(this, dataType, null, valueRank, false, description);
                }
            }

            return arguments;
        }

        private static int GetUnitIdFromXmlNode(XmlNode xmlNode)
        {
            string? unitIdString = xmlNode.SelectSingleNode("descendant::uax:EUInformation/child::uax:UnitId", OpcUaGraph.NamespaceManager)?.InnerText;
            return unitIdString != null ? int.Parse(unitIdString) : 0;
        }

        private static bool AreSameNodeIds(OpcUaNodeId leftNodeId, OpcUaNode leftSource, OpcUaNodeId? rightNodeId, OpcUaNode? rightSource)
        {
            if (rightNodeId == null || rightSource == null)
            {
                return false;
            }

            return leftNodeId.NodeIndex == rightNodeId.NodeIndex &&
                leftSource.DefiningModel.NamespaceUris[leftNodeId.NsIndex] == rightSource.DefiningModel.NamespaceUris[rightNodeId.NsIndex];
        }
    }
}
