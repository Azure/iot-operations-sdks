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

        public WotEvent(string specName, string thingModelName, OpcUaVariable uaVariable, string variableName, string? containedIn, List<string> contains)
        {
            this.uaVariable = uaVariable;
            this.dataSchema = WotDataSchema.Create(uaVariable.DataType, uaVariable.ValueRank, uaVariable, uaVariable.Description, Enumerable.Empty<OpcUaNodeId>());
            this.specName = specName;
            this.thingModelName = thingModelName;
            this.containedIn = containedIn;
            this.contains = contains;
            this.quantityKind = uaVariable.TryGetEngineeringUnits(out OpcUaVariable? engUnitsVariable) ? UnitMapper.GetQuantityKindFromUnitId(engUnitsVariable.UnitId) : null;

            EventName = WotUtil.LegalizeName(variableName);
        }

        public string EventName { get; }

        public bool UsesUnits { get => quantityKind != null; }
    }
}
