// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public partial class WotThingCollection : ITemplateTransform
    {
        public WotThingCollection(OpcUaModelInfo modelInfo, LinkRelRuleEngine linkRelRuleEngine, bool integrate, bool inheritVars, bool includeTDs)
        {
            string specName = SpecMapper.GetSpecNameFromUri(modelInfo.ModelUri);
            this.ThingDescriptions = includeTDs ? modelInfo.NodeIdToObjectMap.Values.Where(o => !modelInfo.ReferencedObjectNodeIds.Contains(o.NodeId)).Select(o => new WotThingDescription(specName, o)).ToList() : new();

            if (integrate)
            {
                this.ThingModels = ModelInfoCloser.ComputeClosure(modelInfo).SelectMany(kvp => kvp.Value.Values.Select(ot => new WotThingModel(SpecMapper.GetSpecNameFromUri(kvp.Key), ot, linkRelRuleEngine, isIntegrated: true, inheritVars: inheritVars))).ToList();
            }
            else
            {
                this.ThingModels = modelInfo.NodeIdToObjectTypeMap.Values.Select(ot => new WotThingModel(specName, ot, linkRelRuleEngine, isIntegrated: false, inheritVars: inheritVars)).ToList();
            }
        }

        public List<WotThingDescription> ThingDescriptions { get; }

        public List<WotThingModel> ThingModels { get; }
    }
}
