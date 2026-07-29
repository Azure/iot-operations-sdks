// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public abstract partial class WotDataSchema : ITemplateTransform
    {
        private static readonly OpcUaNodeId BaseDataTypeNodeId = new OpcUaNodeId(0, 24);
        private static readonly OpcUaNodeId EnumDataTypeNodeId = new OpcUaNodeId(0, 6);

        protected WotDataSchema()
        {
        }

        public static WotDataSchema Create(
            OpcUaNodeId? dataTypeNodeId,
            int valueRank,
            OpcUaNode sourceNode,
            string? description,
            IEnumerable<OpcUaNodeId> ancestors,
            OpcUaNode? typeRefNode = null)
        {
            if (valueRank > 0)
            {
                return new WotDataSchemaArray(dataTypeNodeId, description, valueRank, sourceNode, ancestors, typeRefNode);
            }

            if (dataTypeNodeId == null)
            {
                return new WotDataSchemaPrimitive(BaseDataTypeNodeId, description ?? "Stand-in for an unspecified data type.", dataTypeNode: typeRefNode);
            }

            if (ancestors.Contains(dataTypeNodeId))
            {
                return new WotDataSchemaPrimitive(BaseDataTypeNodeId, $"Stand-in for the infinite remainder of this data structure, which is recursively defined.");
            }

            if (WotDataSchemaPrimitive.IsPrimitive(dataTypeNodeId))
            {
                return new WotDataSchemaPrimitive(dataTypeNodeId, description, dataTypeNode: typeRefNode);
            }

            OpcUaNode dataTypeNode =  sourceNode.GetReferencedOpcUaNode(dataTypeNodeId);

            if (dataTypeNode is OpcUaDataTypeEnum enumNode)
            {
                return new WotDataSchemaPrimitive(EnumDataTypeNodeId, description, WotUtil.LegalizeName(enumNode.EffectiveName), typeRefNode ?? enumNode);
            }
            else if (dataTypeNode is OpcUaDataTypeObject objectNode)
            {
                return new WotDataSchemaObject(typeRefNode ?? objectNode, objectNode.Description ?? description, WotUtil.LegalizeName(objectNode.EffectiveName), objectNode.GetAllObjectFields(), ancestors.Append(dataTypeNodeId), objectNode.IsUnion);
            }
            else if (dataTypeNode is OpcUaDataTypeSubtype subtypeNode)
            {
                OpcUaNodeId? primitiveBaseTypeNodeId = subtypeNode.BaseTypes.FirstOrDefault(t => WotDataSchemaPrimitive.IsPrimitive(t));
                if (primitiveBaseTypeNodeId != null)
                {
                    return new WotDataSchemaPrimitive(primitiveBaseTypeNodeId, description, dataTypeNode: typeRefNode);
                }

                return Create(subtypeNode.BaseTypes.First(), 0, subtypeNode, description, ancestors.Append(dataTypeNodeId), typeRefNode);
            }
            else
            {
                throw new Exception($"Node ID '{dataTypeNode.NodeId}' passed to WotDataSchema.Create() is not an OpcUaDataType.");
            }
        }
    }
}
