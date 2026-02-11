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

        public WotAction(string specName, string thingModelName, OpcUaMethod uaMethod)
        {
            this.uaMethod = uaMethod;
            this.specName = specName;
            this.thingModelName = thingModelName;
            this.actionName = WotUtil.LegalizeName(uaMethod.EffectiveName);

            this.inputSchema = uaMethod.TryGetInputArguments(out OpcUaVariable? inputArgsVariable) ? new WotDataSchemaObject(inputArgsVariable, null, null, inputArgsVariable.Arguments, 0) : null;
            this.outputSchema = uaMethod.TryGetOutputArguments(out OpcUaVariable? outputArgsVariable) ? new WotDataSchemaObject(outputArgsVariable, null, null, outputArgsVariable.Arguments, 0) : null;
        }
    }
}
