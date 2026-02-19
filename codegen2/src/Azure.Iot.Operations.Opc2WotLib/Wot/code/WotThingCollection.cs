// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public partial class WotThingCollection : ITemplateTransform
    {
        private List<WotThingModel> thingModels;

        public WotThingCollection(OpcUaModelInfo modelInfo, LinkRelRuleEngine linkRelRuleEngine, bool integrate)
        {
            if (integrate)
            {
                this.thingModels = ModelInfoCloser.ComputeClosure(modelInfo).SelectMany(kvp => kvp.Value.Values.Select(ot => new WotThingModel(SpecMapper.GetSpecNameFromUri(kvp.Key), ot, linkRelRuleEngine, isIntegrated: true))).ToList();
            }
            else
            {
                string specName = SpecMapper.GetSpecNameFromUri(modelInfo.ModelUri);
                this.thingModels = modelInfo.NodeIdToObjectTypeMap.Values.Select(ot => new WotThingModel(specName, ot, linkRelRuleEngine, isIntegrated: false)).ToList();
            }
        }
    }
}
