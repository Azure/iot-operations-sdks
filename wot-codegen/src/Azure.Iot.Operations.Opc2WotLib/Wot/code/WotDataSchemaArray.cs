// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;

    public partial class WotDataSchemaArray : WotDataSchema
    {
        private string? description;
        private WotDataSchema itemDataSchema;

        public WotDataSchemaArray(OpcUaNodeId? dataTypeNodeId, string? description, int valueRank, OpcUaNode sourceNode, IEnumerable<OpcUaNodeId> ancestors)
        {
            if (valueRank <= 0)
            {
                throw new System.Exception($"WotDataSchemaArray constructor called with non-positive valueRank of {valueRank}");
            }

            this.description = description;
            itemDataSchema = WotDataSchema.Create(dataTypeNodeId, valueRank - 1, sourceNode, null, ancestors);
        }
    }
}
