// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public partial class WotDataTypeModel : ITemplateTransform
    {
        private string modelUri;
        private string specName;
        private string thingName;
        private string id;
        private List<KeyValuePair<string, WotDataSchema>> schemaDefinitions;

        public WotDataTypeModel(string modelUri, string specName, IEnumerable<OpcUaDataType> dataTypes)
        {
            this.modelUri = modelUri;
            this.specName = specName;
            this.thingName = WotUtil.LegalizeName("DataTypes", specName);
            this.id = WotUtil.GetThingModelId(modelUri, this.thingName);

            this.schemaDefinitions = dataTypes
                .Where(dt => !dt.IsDeprecated && !dt.NodeId.IsBuiltInDataType)
                .OrderBy(dt => dt.EffectiveName, StringComparer.Ordinal)
                .Select(dt => new KeyValuePair<string, WotDataSchema>(dt.EffectiveName, CreateSchema(dt)))
                .ToList();
        }

        public WotDataTypeModel(string modelUri, string specName, IEnumerable<OpcUaVariableType> variableTypes)
        {
            this.modelUri = modelUri;
            this.specName = specName;
            this.thingName = WotUtil.LegalizeName("VariableTypes", specName);
            this.id = WotUtil.GetThingModelId(modelUri, this.thingName);

            Dictionary<OpcUaVariableType, string> schemaNames = WotVariableTypeSchema.GetSchemaNames(
                variableTypes.Where(vt => vt.IsSchemaEligible));

            this.schemaDefinitions = schemaNames
                .OrderBy(kvp => kvp.Value, StringComparer.Ordinal)
                .Select(kvp => new KeyValuePair<string, WotDataSchema>(kvp.Value, WotVariableTypeSchema.Create(kvp.Key)))
                .ToList();
        }

        public bool HasSchemaDefinitions => this.schemaDefinitions.Count > 0;

        public string FileName => $"{this.thingName}.TM.json";

        public IEnumerable<WotThingDocument> GetDocuments()
        {
            return this.schemaDefinitions.Select(schemaDefinition =>
            {
                string schemaThingName = WotUtil.LegalizeName(schemaDefinition.Key, this.specName);
                WotDataTypeModel schemaModel = new WotDataTypeModel(
                    this.modelUri,
                    schemaThingName,
                    new List<KeyValuePair<string, WotDataSchema>> { schemaDefinition });
                return WotThingDocument.Create(schemaModel.FileName, schemaModel.TransformText());
            });
        }

        private WotDataTypeModel(string modelUri, string thingName, List<KeyValuePair<string, WotDataSchema>> schemaDefinitions)
        {
            this.modelUri = modelUri;
            this.specName = string.Empty;
            this.thingName = thingName;
            this.id = WotUtil.GetThingModelId(modelUri, thingName);
            this.schemaDefinitions = schemaDefinitions;
        }

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
