/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

#nullable enable

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    [System.CodeDom.Compiler.GeneratedCode("Azure.Iot.Operations.ProtocolCompiler", "0.10.0.0")]
    public partial class DetectedAsset : IJsonOnDeserialized, IJsonOnSerializing
    {
        /// <summary>
        /// A reference to the asset endpoint profile.
        /// </summary>
        [JsonPropertyName("assetEndpointProfileRef")]
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        [JsonRequired]
        public string AssetEndpointProfileRef { get; set; } = default!;

        /// <summary>
        /// Name of the asset if available.
        /// </summary>
        [JsonPropertyName("assetName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? AssetName { get; set; } = default;

        /// <summary>
        /// Array of datasets that are part of the asset. Each dataset spec describes the datapoints that make up the set.
        /// </summary>
        [JsonPropertyName("datasets")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<DetectedAssetDatasetSchemaElementSchema>? Datasets { get; set; } = default;

        /// <summary>
        /// The 'defaultDatasetsConfiguration' Field.
        /// </summary>
        [JsonPropertyName("defaultDatasetsConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? DefaultDatasetsConfiguration { get; set; } = default;

        /// <summary>
        /// The 'defaultEventsConfiguration' Field.
        /// </summary>
        [JsonPropertyName("defaultEventsConfiguration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? DefaultEventsConfiguration { get; set; } = default;

        /// <summary>
        /// The 'defaultTopic' Field.
        /// </summary>
        [JsonPropertyName("defaultTopic")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Topic? DefaultTopic { get; set; } = default;

        /// <summary>
        /// URI to the documentation of the asset.
        /// </summary>
        [JsonPropertyName("documentationUri")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? DocumentationUri { get; set; } = default;

        /// <summary>
        /// Array of events that are part of the asset. Each event can reference an asset type capability and have per-event configuration.
        /// </summary>
        [JsonPropertyName("events")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<DetectedAssetEventSchemaElementSchema>? Events { get; set; } = default;

        /// <summary>
        /// The 'hardwareRevision' Field.
        /// </summary>
        [JsonPropertyName("hardwareRevision")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? HardwareRevision { get; set; } = default;

        /// <summary>
        /// Asset manufacturer name.
        /// </summary>
        [JsonPropertyName("manufacturer")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Manufacturer { get; set; } = default;

        /// <summary>
        /// URI to the manufacturer of the asset.
        /// </summary>
        [JsonPropertyName("manufacturerUri")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? ManufacturerUri { get; set; } = default;

        /// <summary>
        /// Asset model name.
        /// </summary>
        [JsonPropertyName("model")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Model { get; set; } = default;

        /// <summary>
        /// Asset product code.
        /// </summary>
        [JsonPropertyName("productCode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? ProductCode { get; set; } = default;

        /// <summary>
        /// Asset serial number.
        /// </summary>
        [JsonPropertyName("serialNumber")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? SerialNumber { get; set; } = default;

        /// <summary>
        /// Revision number of the software.
        /// </summary>
        [JsonPropertyName("softwareRevision")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? SoftwareRevision { get; set; } = default;

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (AssetEndpointProfileRef is null)
            {
                throw new ArgumentNullException("assetEndpointProfileRef field cannot be null");
            }
        }

        void IJsonOnSerializing.OnSerializing()
        {
            if (AssetEndpointProfileRef is null)
            {
                throw new ArgumentNullException("assetEndpointProfileRef field cannot be null");
            }
        }
    }
}
