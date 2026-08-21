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
        private string? typeRef;
        private List<KeyValuePair<string, WotDataSchema>> schemaDefinitions;
        private Dictionary<string, string> schemaTypeRefs;

        public WotDataTypeModel(string modelUri, string specName, IEnumerable<OpcUaDataType> dataTypes)
        {
            this.modelUri = modelUri;
            this.specName = specName;
            this.thingName = WotUtil.LegalizeName("DataTypes", specName);
            this.id = WotUtil.GetThingModelId(modelUri, this.thingName);
            this.typeRef = null;

            List<OpcUaDataType> includedDataTypes = dataTypes
                .Where(dt => !dt.IsDeprecated && !dt.NodeId.IsBuiltInDataType)
                .OrderBy(dt => dt.EffectiveName, StringComparer.Ordinal)
                .ToList();
            this.schemaDefinitions = includedDataTypes
                .Select(dt => new KeyValuePair<string, WotDataSchema>(dt.EffectiveName, CreateSchema(dt)))
                .ToList();
            this.schemaTypeRefs = includedDataTypes.ToDictionary(dt => dt.EffectiveName, dt => dt.GetTypeRef(), StringComparer.Ordinal);
        }

        public WotDataTypeModel(string modelUri, string specName, IEnumerable<OpcUaVariableType> variableTypes)
        {
            this.modelUri = modelUri;
            this.specName = specName;
            this.thingName = WotUtil.LegalizeName("VariableTypes", specName);
            this.id = WotUtil.GetThingModelId(modelUri, this.thingName);
            this.typeRef = null;

            Dictionary<OpcUaVariableType, string> schemaNames = WotVariableTypeSchema.GetSchemaNames(
                variableTypes.Where(vt => vt.IsSchemaEligible));

            this.schemaDefinitions = schemaNames
                .OrderBy(kvp => kvp.Value, StringComparer.Ordinal)
                .Select(kvp => new KeyValuePair<string, WotDataSchema>(kvp.Value, WotVariableTypeSchema.Create(kvp.Key)))
                .ToList();
            this.schemaTypeRefs = schemaNames.ToDictionary(kvp => kvp.Value, kvp => kvp.Key.GetTypeRef(), StringComparer.Ordinal);
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
                    this.schemaTypeRefs[schemaDefinition.Key],
                    new List<KeyValuePair<string, WotDataSchema>> { schemaDefinition });
                return WotThingDocument.Create(schemaModel.FileName, schemaModel.TransformText());
            });
        }

        private WotDataTypeModel(string modelUri, string thingName, string typeRef, List<KeyValuePair<string, WotDataSchema>> schemaDefinitions)
        {
            this.modelUri = modelUri;
            this.specName = string.Empty;
            this.thingName = thingName;
            this.id = WotUtil.GetThingModelId(modelUri, thingName);
            this.typeRef = typeRef;
            this.schemaDefinitions = schemaDefinitions;
            this.schemaTypeRefs = new Dictionary<string, string>(StringComparer.Ordinal);
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
