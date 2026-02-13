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

        private OpcUaVariable uaVariable;
        private WotDataSchema dataSchema;
        private string specName;
        private string thingModelName;
        private string propertyName;
        private string? containedIn;
        private List<string> contains;

        public WotProperty(string specName, string thingModelName, OpcUaVariable uaVariable, string variableName, string? containedIn, List<string> contains)
        {
            this.uaVariable = uaVariable;
            this.dataSchema = WotDataSchema.Create(uaVariable.DataType, uaVariable.ValueRank, uaVariable, uaVariable.Description, Enumerable.Empty<OpcUaNodeId>());
            this.specName = specName;
            this.thingModelName = thingModelName;
            this.propertyName = WotUtil.LegalizeName(variableName);
            this.containedIn = containedIn;
            this.contains = contains;

            ReadOnly = (uaVariable.AccessLevel & writeMask) == 0;
        }

        public bool ReadOnly { get; }
    }
}
