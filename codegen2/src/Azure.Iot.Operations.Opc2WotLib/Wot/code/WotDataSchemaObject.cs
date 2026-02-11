// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public partial class WotDataSchemaObject : WotDataSchema
    {
        private string? description;
        private string? schemaName;
        private string typeRef;
        private Dictionary<string, WotDataSchema> fieldDataSchemas;
        private List<string> requiredFieldNames;

        public WotDataSchemaObject(OpcUaNode containingNode, string? description, string? schemaName, Dictionary<string, OpcUaObjectField> fields, int depth)
        {
            this.description = description;
            this.schemaName = schemaName;
            this.typeRef = $"nsu={containingNode.NodeIdNamespace};i={containingNode.NodeId.NodeIndex}";

            fieldDataSchemas =  fields.ToDictionary(
                field => field.Key,
                field => WotDataSchema.Create(field.Value.DataType, field.Value.ValueRank, containingNode, depth + 1, field.Value.Description));

            requiredFieldNames = fields
                .Where(field => !field.Value.IsOptional)
                .Select(field => field.Key)
                .ToList();
        }
    }
}
