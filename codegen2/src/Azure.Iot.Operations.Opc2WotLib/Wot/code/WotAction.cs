// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Linq;

    public partial class WotAction : ITemplateTransform
    {
        private OpcUaMethod uaMethod;
        private string specName;
        private string thingModelName;
        private string actionName;
        private WotDataSchema? inputSchema;
        private WotDataSchema? outputSchema;

        public WotAction(string specName, string thingModelName, OpcUaMethod uaMethod, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap)
        {
            this.uaMethod = uaMethod;
            this.specName = specName;
            this.thingModelName = thingModelName;
            this.actionName = WotUtil.LegalizeName(uaMethod.EffectiveName);

            OpcUaVariable? inputArgsVariable = uaMethod.GetProperties(nsUriToNsInfoMap).OfType<OpcUaVariable>().FirstOrDefault(v => v.BrowseName.Name == "InputArguments");
            OpcUaVariable? outputArgsVariable = uaMethod.GetProperties(nsUriToNsInfoMap).OfType<OpcUaVariable>().FirstOrDefault(v => v.BrowseName.Name == "OutputArguments");

            this.inputSchema = inputArgsVariable == null ? null : new WotDataSchemaObject(inputArgsVariable, null, null, inputArgsVariable.Arguments, nsUriToNsInfoMap, 0);
            this.outputSchema = outputArgsVariable == null ? null : new WotDataSchemaObject(outputArgsVariable, null, null, outputArgsVariable.Arguments, nsUriToNsInfoMap, 0);
        }
    }
}
