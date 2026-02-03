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

        public WotProperty(string specName, string thingModelName, OpcUaVariable uaVariable, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap)
        {
            this.uaVariable = uaVariable;
            this.dataSchema = WotDataSchema.Create(uaVariable.DataType, uaVariable.ValueRank, uaVariable, nsUriToNsInfoMap, 0, uaVariable.Description);
            this.specName = specName;
            this.thingModelName = thingModelName;
            this.propertyName = WotUtil.LegalizeName(uaVariable.EffectiveName);

            ReadOnly = (uaVariable.AccessLevel & writeMask) == 0;
        }

        public bool ReadOnly { get; }
    }
}
