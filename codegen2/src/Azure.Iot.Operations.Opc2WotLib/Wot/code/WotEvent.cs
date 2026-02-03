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
        private string eventName;

        public WotEvent(string specName, string thingModelName, OpcUaVariable uaVariable, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap)
        {
            this.uaVariable = uaVariable;
            this.dataSchema = WotDataSchema.Create(uaVariable.DataType, uaVariable.ValueRank, uaVariable, nsUriToNsInfoMap, 0, uaVariable.Description);
            this.specName = specName;
            this.thingModelName = thingModelName;
            this.eventName = WotUtil.LegalizeName(uaVariable.EffectiveName);
        }
    }
}
