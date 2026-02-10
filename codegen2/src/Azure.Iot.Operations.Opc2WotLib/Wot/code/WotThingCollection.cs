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

        public WotThingCollection(OpcUaModelInfo modelInfo, Dictionary<string, OpcUaNamespaceInfo> nsUriToNsInfoMap, LinkRelRuleEngine linkRelRuleEngine)
        {
            string specName = SpecMapper.GetSpecNameFromUri(modelInfo.ModelUri);
            this.thingModels = modelInfo.NodeIdToObjectTypeMap.Values.Select(ot => new WotThingModel(specName, ot, nsUriToNsInfoMap, linkRelRuleEngine)).ToList();
        }
    }
}
