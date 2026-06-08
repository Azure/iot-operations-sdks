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
        private WotDataSchema? inputSchema;
        private WotDataSchema? outputSchema;
        private bool inDescription;

        public WotAction(string specName, string thingModelName, OpcUaMethod uaMethod, bool inDescription)
        {
            this.uaMethod = uaMethod;
            this.specName = specName;
            this.thingModelName = thingModelName;

            this.inputSchema = uaMethod.TryGetInputArguments(out OpcUaVariable? inputArgsVariable) ? new WotDataSchemaObject(inputArgsVariable, null, null, inputArgsVariable.Arguments, Enumerable.Empty<OpcUaNodeId>(), isUnion: false) : null;
            this.outputSchema = uaMethod.TryGetOutputArguments(out OpcUaVariable? outputArgsVariable) ? new WotDataSchemaObject(outputArgsVariable, null, null, outputArgsVariable.Arguments, Enumerable.Empty<OpcUaNodeId>(), isUnion: false) : null;
            this.inDescription = inDescription;

            ActionName = WotUtil.LegalizeName(uaMethod.EffectiveName);
            IsMandatory = uaMethod.IsMandatory;
        }

        public string ActionName { get; }

        public bool IsMandatory { get; }
    }
}
