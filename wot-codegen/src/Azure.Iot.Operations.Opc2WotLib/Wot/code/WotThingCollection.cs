// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public partial class WotThingCollection : ITemplateTransform
    {
        public WotThingCollection(OpcUaGraph opcUaGraph, OpcUaModelInfo modelInfo, LinkRelRuleEngine linkRelRuleEngine, bool integrate, bool inheritVars, bool includeTDs)
        {
            string specName = SpecMapper.GetSpecNameFromUri(modelInfo.ModelUri);
            this.ThingDescriptions = includeTDs ? modelInfo.NodeIdToObjectMap.Values.Where(o => !modelInfo.ReferencedObjectNodeIds.Contains(o.NodeId)).Select(o => new WotThingDescription(specName, o)).ToList() : new();

            if (integrate)
            {
                Dictionary<string, Dictionary<OpcUaNodeId, OpcUaObjectType>> closure = ModelInfoCloser.ComputeClosure(modelInfo);
                this.ThingModels = closure.SelectMany(kvp => kvp.Value.Values.Select(ot => new WotThingModel(SpecMapper.GetSpecNameFromUri(kvp.Key), ot, linkRelRuleEngine, isIntegrated: true, inheritVars: inheritVars))).ToList();
                this.DataTypeModels = opcUaGraph.GetRequiredModelClosure(modelInfo)
                    .Select(requiredModel => new WotDataTypeModel(SpecMapper.GetSpecNameFromUri(requiredModel.ModelUri), requiredModel.NodeIdToDataTypeMap.Values))
                    .Where(dtm => dtm.HasSchemaDefinitions)
                    .ToList();
                this.VariableTypeModels = opcUaGraph.GetRequiredModelClosure(modelInfo)
                    .Select(requiredModel => new WotDataTypeModel(SpecMapper.GetSpecNameFromUri(requiredModel.ModelUri), requiredModel.NodeIdToVariableTypeMap.Values))
                    .Where(vtm => vtm.HasSchemaDefinitions)
                    .ToList();
            }
            else
            {
                this.ThingModels = modelInfo.NodeIdToObjectTypeMap.Values.Select(ot => new WotThingModel(specName, ot, linkRelRuleEngine, isIntegrated: false, inheritVars: inheritVars)).ToList();
                this.DataTypeModels = new List<WotDataTypeModel> { new WotDataTypeModel(specName, modelInfo.NodeIdToDataTypeMap.Values) }
                    .Where(dtm => dtm.HasSchemaDefinitions)
                    .ToList();
                this.VariableTypeModels = new List<WotDataTypeModel> { new WotDataTypeModel(specName, modelInfo.NodeIdToVariableTypeMap.Values) }
                    .Where(vtm => vtm.HasSchemaDefinitions)
                    .ToList();
            }
        }

        public List<WotThingDescription> ThingDescriptions { get; }

        public List<WotThingModel> ThingModels { get; }

        public List<WotDataTypeModel> DataTypeModels { get; }

        public List<WotDataTypeModel> VariableTypeModels { get; }
    }
}
