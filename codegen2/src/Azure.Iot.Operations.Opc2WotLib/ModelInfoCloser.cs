// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Linq;

    public static class ModelInfoCloser
    {
        public static Dictionary<string, Dictionary<OpcUaNodeId, OpcUaObjectType>> ComputeClosure(OpcUaModelInfo modelInfo)
        {
            Dictionary<string, Dictionary<OpcUaNodeId, OpcUaObjectType>> modelUriToNodeIdToObjectTypeMap = new();

            foreach (KeyValuePair<OpcUaNodeId, OpcUaObjectType> nodeIdAndObjectType in modelInfo.NodeIdToObjectTypeMap)
            {
                CollateBaseAndReferencedObjectTypes(nodeIdAndObjectType.Value, modelUriToNodeIdToObjectTypeMap);
            }

            return modelUriToNodeIdToObjectTypeMap;
        }

        private static void CollateBaseAndReferencedObjectTypes(OpcUaObjectType uaObjectType, Dictionary<string, Dictionary<OpcUaNodeId, OpcUaObjectType>> modelUriToNodeIdToObjectTypeMap)
        {
            if (!modelUriToNodeIdToObjectTypeMap.TryGetValue(uaObjectType.DefiningModel.ModelUri, out Dictionary<OpcUaNodeId, OpcUaObjectType>? nodeIdToObjectTypeMap))
            {
                nodeIdToObjectTypeMap = new Dictionary<OpcUaNodeId, OpcUaObjectType>();
                modelUriToNodeIdToObjectTypeMap[uaObjectType.DefiningModel.ModelUri] = nodeIdToObjectTypeMap;
            }
            if (!nodeIdToObjectTypeMap.TryAdd(uaObjectType.NodeId, uaObjectType))
            {
                return;
            }

            foreach (OpcUaObjectType baseObjectType in uaObjectType.BaseModels)
            {
                CollateBaseAndReferencedObjectTypes(baseObjectType, modelUriToNodeIdToObjectTypeMap);
            }

            foreach ((OpcUaNodeId _, OpcUaObject targetObject) in uaObjectType.TypeAndObjectOfReferences.Where(t => t.Item1.NsIndex != 0 || t.Item1.IsComponentReference))
            {
                CollateBaseAndReferencedObjectTypes((OpcUaObjectType)targetObject.GetReferencedOpcUaNode(targetObject.HasTypeDefinitionNodeId!), modelUriToNodeIdToObjectTypeMap);
            }
        }
    }
}
