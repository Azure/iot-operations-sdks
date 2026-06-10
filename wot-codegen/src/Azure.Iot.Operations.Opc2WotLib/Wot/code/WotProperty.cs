// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public partial class WotProperty : ITemplateTransform
    {
        // bits defined here: https://reference.opcfoundation.org/Core/Part3/v104/docs/8.57
        private const int bitPosWrite = 1;

        private static readonly int writeMask = 1 << bitPosWrite;

        private WotDataSchema dataSchema;
        private string? browseNamespace;
        private bool isPlaceholder;
        private string specName;
        private string thingModelName;
        private string? containedIn;
        private List<string> contains;
        private string? quantityKind;
        private bool inDescription;

        public WotProperty(string specName, string thingModelName, OpcUaVariable uaVariable, string variableName, string? containedIn, List<string> contains, bool inDescription)
        {
            this.dataSchema = WotDataSchema.Create(uaVariable.DataType, uaVariable.ValueRank, uaVariable, uaVariable.Description, Enumerable.Empty<OpcUaNodeId>());
            this.browseNamespace = uaVariable.BrowseNamespace;
            this.isPlaceholder = uaVariable.IsPlaceholder;
            this.specName = specName;
            this.thingModelName = thingModelName;
            this.containedIn = containedIn;
            this.contains = contains;
            this.quantityKind = uaVariable.TryGetEngineeringUnits(out OpcUaVariable? engUnitsVariable) ? UnitMapper.GetQuantityKindFromUnitId(engUnitsVariable.UnitId) : null;
            this.inDescription = inDescription;

            PropertyName = WotUtil.LegalizeName(variableName);
            ReadOnly = (uaVariable.AccessLevel & writeMask) == 0;
            IsMandatory = uaVariable.IsMandatory;
        }

        public WotProperty(string specName, string thingModelName, string propertyName, OpcUaNodeId dataTypeNodeId, bool readOnly, bool isMandatory, string? description, bool inDescription)
        {
            this.dataSchema = new WotDataSchemaPrimitive(dataTypeNodeId, description);
            this.browseNamespace = null;
            this.isPlaceholder = false;
            this.specName = specName;
            this.thingModelName = thingModelName;
            this.containedIn = null;
            this.contains = new List<string>();
            this.quantityKind = null;
            this.inDescription = inDescription;

            PropertyName = propertyName;
            ReadOnly = readOnly;
            IsMandatory = isMandatory;
        }

        public string PropertyName { get; }

        public bool ReadOnly { get; }

        public bool IsMandatory { get; }

        public bool UsesUnits { get => quantityKind != null; }
    }
}
