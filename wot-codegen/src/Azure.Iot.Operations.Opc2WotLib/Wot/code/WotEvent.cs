// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public partial class WotEvent : ITemplateTransform
    {
        private OpcUaVariable uaVariable;
        private WotDataSchema dataSchema;
        private string specName;
        private string thingModelName;
        private string? containedIn;
        private List<string> contains;
        private string? quantityKind;
        private bool inDescription;

        public WotEvent(string specName, string thingModelName, OpcUaVariable uaVariable, string variableName, string? containedIn, List<string> contains, bool inDescription, string? variableTypeSchemaName = null)
        {
            this.uaVariable = uaVariable;
            this.dataSchema = variableTypeSchemaName != null
                ? new WotDataSchemaReference(variableTypeSchemaName)
                : WotDataSchema.Create(uaVariable.EffectiveDataType, uaVariable.EffectiveValueRank, uaVariable.EffectiveDataTypeSource, uaVariable.Description, Enumerable.Empty<OpcUaNodeId>(), uaVariable.CustomVariableType);
            this.specName = specName;
            this.thingModelName = thingModelName;
            this.containedIn = containedIn;
            this.contains = contains;
            this.quantityKind = uaVariable.TryGetEngineeringUnits(out OpcUaVariable? engUnitsVariable) ? UnitMapper.GetQuantityKindFromUnitId(engUnitsVariable.UnitId) : null;
            this.inDescription = inDescription;

            EventName = WotUtil.LegalizeName(variableName);
            IsMandatory = uaVariable.IsMandatory;
        }

        public string EventName { get; }

        public bool IsMandatory { get; }

        public bool UsesUnits { get => quantityKind != null; }
    }
}
