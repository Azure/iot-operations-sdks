// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public partial class WotDataTypeModel : ITemplateTransform
    {
        private string thingName;
        private List<KeyValuePair<string, WotDataSchema>> schemaDefinitions;

        public WotDataTypeModel(string specName, IEnumerable<OpcUaDataType> dataTypes)
        {
            this.thingName = WotUtil.LegalizeName("DataTypes", specName);

            this.schemaDefinitions = dataTypes
                .Where(dt => !dt.IsDeprecated && !dt.NodeId.IsBuiltInDataType)
                .OrderBy(dt => dt.EffectiveName, StringComparer.Ordinal)
                .Select(dt => new KeyValuePair<string, WotDataSchema>(dt.EffectiveName, CreateSchema(dt)))
                .ToList();
        }

        public WotDataTypeModel(string specName, IEnumerable<OpcUaVariableType> variableTypes)
        {
            this.thingName = WotUtil.LegalizeName("VariableTypes", specName);

            Dictionary<OpcUaVariableType, string> schemaNames = WotVariableTypeSchema.GetSchemaNames(
                variableTypes.Where(vt => !vt.IsDeprecated && vt.DefiningModel.ModelUri != OpcUaGraph.OpcUaCoreModelUri));

            this.schemaDefinitions = schemaNames
                .OrderBy(kvp => kvp.Value, StringComparer.Ordinal)
                .Select(kvp => new KeyValuePair<string, WotDataSchema>(kvp.Value, WotVariableTypeSchema.Create(kvp.Key)))
                .ToList();
        }

        public bool HasSchemaDefinitions => this.schemaDefinitions.Count > 0;

        private static WotDataSchema CreateSchema(OpcUaDataType dataType)
        {
            switch (dataType)
            {
                case OpcUaDataTypeEnum dataTypeEnum:
                    return new WotDataSchemaEnum(dataTypeEnum);
                case OpcUaDataTypeObject dataTypeObject:
                    return new WotDataSchemaObject(dataTypeObject, dataTypeObject.Description, null, dataTypeObject.GetAllObjectFields(), new[] { dataTypeObject.NodeId }, dataTypeObject.IsUnion);
                case OpcUaDataTypeSubtype dataTypeSubtype:
                    return WotDataSchema.Create(dataTypeSubtype.NodeId, 0, dataTypeSubtype, dataTypeSubtype.Description, Enumerable.Empty<OpcUaNodeId>());
                default:
                    throw new Exception($"Unrecognized OpcUaDataType kind '{dataType.GetType().Name}' for node ID '{dataType.NodeId}'.");
            }
        }
    }
}
