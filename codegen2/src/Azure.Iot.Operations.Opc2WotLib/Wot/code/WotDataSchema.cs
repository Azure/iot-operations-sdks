// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public abstract partial class WotDataSchema : ITemplateTransform
    {
        private const int depthLimit = 10;

        private static readonly OpcUaNodeId BaseDataTypeNodeId = new OpcUaNodeId(0, 24);
        private static readonly OpcUaNodeId EnumDataTypeNodeId = new OpcUaNodeId(0, 6);

        protected WotDataSchema()
        {
        }

        public static WotDataSchema Create(OpcUaNodeId? dataTypeNodeId, int valueRank, OpcUaNode sourceNode, int depth, string? description)
        {
            if (depth > depthLimit)
            {
                return new WotDataSchemaPrimitive(BaseDataTypeNodeId, $"Stand-in for the remainder of this data structure, which is deeper than the limit of {depthLimit} levels.");
            }

            if (valueRank > 0)
            {
                return new WotDataSchemaArray(dataTypeNodeId, description, valueRank, sourceNode, depth);
            }

            if (dataTypeNodeId == null)
            {
                return new WotDataSchemaPrimitive(BaseDataTypeNodeId, description ?? "Stand-in for an unspecified data type.");
            }

            if (WotDataSchemaPrimitive.IsPrimitive(dataTypeNodeId))
            {
                return new WotDataSchemaPrimitive(dataTypeNodeId, description);
            }

            OpcUaNode dataTypeNode =  sourceNode.GetReferencedOpcUaNode(dataTypeNodeId);

            if (dataTypeNode is OpcUaDataTypeEnum enumNode)
            {
                return new WotDataSchemaPrimitive(EnumDataTypeNodeId, description, WotUtil.LegalizeName(enumNode.EffectiveName), enumNode);
            }
            else if (dataTypeNode is OpcUaDataTypeObject objectNode)
            {
                return new WotDataSchemaObject(objectNode, objectNode.Description ?? description, WotUtil.LegalizeName(objectNode.EffectiveName), objectNode.ObjectFields, depth);
            }
            else if (dataTypeNode is OpcUaDataTypeSubtype subtypeNode)
            {
                OpcUaNodeId? primitiveBaseTypeNodeId = subtypeNode.BaseTypes.FirstOrDefault(t => WotDataSchemaPrimitive.IsPrimitive(t));
                if (primitiveBaseTypeNodeId != null)
                {
                    return new WotDataSchemaPrimitive(primitiveBaseTypeNodeId, description);
                }

                return Create(subtypeNode.BaseTypes.First(), 0, subtypeNode, depth, description);
            }
            else
            {
                throw new Exception($"Node ID '{dataTypeNode.NodeId}' passed to WotDataSchema.Create() is not an OpcUaDataType.");
            }
        }
    }
}
