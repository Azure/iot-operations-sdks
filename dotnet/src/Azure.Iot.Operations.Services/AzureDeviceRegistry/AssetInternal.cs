// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    public record AssetInternal
    {
        /// <summary>
        /// Globally unique, immutable, non-reusable id.
        /// </summary>
        public string? Uuid { get; init; }

        /// <summary>
        /// Enabled/Disabled status of the asset.
        /// </summary>
        public bool? Enabled { get; init; }

        /// <summary>
        /// Asset id provided by the customer.
        /// </summary>
        public string? ExternalAssetId { get; init; }

        /// <summary>
        /// Human-readable display name.
        /// </summary>
        public string? DisplayName { get; init; }

        /// <summary>
        /// Human-readable description of the asset.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// A reference to the asset endpoint profile (connection information) used by brokers to connect to an endpoint that provides data points for this asset. Must have the format <ModuleCR.metadata.namespace>/<ModuleCR.metadata.name>.
        /// </summary>
        public string? AssetEndpointProfileRef { get; init; }

        /// <summary>
        /// A value that is incremented each time the resource is modified.
        /// </summary>
        public long? Version { get; init; }

        /// <summary>
        /// Asset manufacturer name.
        /// </summary>
        public string? Manufacturer { get; init; }

        /// <summary>
        /// Asset manufacturer URI.
        /// </summary>
        public string? ManufacturerUri { get; init; }

        /// <summary>
        /// Asset model name.
        /// </summary>
        public string? Model { get; init; }

        /// <summary>
        /// Asset product code.
        /// </summary>
        public string? ProductCode { get; init; }

        /// <summary>
        /// Revision number of the hardware.
        /// </summary>
        public string? HardwareRevision { get; init; }

        /// <summary>
        /// Revision number of the software.
        /// </summary>
        public string? SoftwareRevision { get; init; }

        /// <summary>
        /// Reference to the documentation.
        /// </summary>
        public string? DocumentationUri { get; init; }

        /// <summary>
        /// Asset serial number.
        /// </summary>
        public string? SerialNumber { get; init; }

        /// <summary>
        /// A set of key-value pairs that contain custom attributes set by the customer.
        /// </summary>
        public JsonDocument? Attributes { get; init; }

        /// <summary>
        /// Reference to a list of discovered assets. Populated only if the asset has been created from discovery flow.
        /// </summary>
        public string[]? DiscoveredAssetRefs { get; init; }

        /// <summary>
        /// Protocol-specific default configuration for all datasets. Each dataset can have its own configuration that overrides the default settings here.
        /// </summary>
        public string? DefaultDatasetsConfiguration { get; init; }

        /// <summary>
        /// Protocol-specific default configuration for all data sets. Each data set can have its own configuration that overrides the default settings here. This assumes that each asset instance has one protocol.
        /// </summary>
        public string? DefaultEventsConfiguration { get; init; }

        /// <summary>
        /// Object that describes the topic information.
        /// </summary>
        public Topic? DefaultTopic { get; init; }

        /// <summary>
        /// The mapping of dataset names to datasets that are part of the asset. Each dataset can have per-dataset configuration.
        /// </summary>
        public DatasetInternal[]? Datasets { get; init; }

        /// <summary>
        /// Array of events that are part of the asset. Each event can reference an asset type capability and have per-event configuration.
        /// </summary>
        public EventInternal[]? Events { get; init; }

        /// <summary>
        /// Read only object to reflect changes that have occurred on the Edge. Similar to Kubernetes status property for custom resources.
        /// </summary>
        public Status? Status { get; init; }

        /// <summary>
        /// Provisioning state of the resource.
        /// </summary>
        public string? ProvisioningState { get; init; }

        internal Asset ToPublic()
        {
            JsonDocumentOptions options = new JsonDocumentOptions()
            {
                AllowTrailingCommas = true,
            };

            Uuid = internalAsset.Uuid;
            Enabled = internalAsset.Enabled;
            ExternalAssetId = internalAsset.ExternalAssetId;
            DisplayName = internalAsset.DisplayName;
            Description = internalAsset.Description;
            AssetEndpointProfileRef = internalAsset.AssetEndpointProfileRef;
            Version = internalAsset.Version;
            Manufacturer = internalAsset.Manufacturer;
            ManufacturerUri = internalAsset.ManufacturerUri;
            Model = internalAsset.Model;
            ProductCode = internalAsset.ProductCode;
            HardwareRevision = internalAsset.HardwareRevision;
            SoftwareRevision = internalAsset.SoftwareRevision;
            DocumentationUri = internalAsset.DocumentationUri;
            SerialNumber = internalAsset.SerialNumber;
            Attributes = internalAsset.Attributes;
            DiscoveredAssetRefs = internalAsset.DiscoveredAssetRefs;
            DefaultDatasetsConfiguration = internalAsset.DefaultDatasetsConfiguration != null ? JsonDocument.Parse(internalAsset.DefaultDatasetsConfiguration, options) : null;
            DefaultEventsConfiguration = internalAsset.DefaultEventsConfiguration != null ? JsonDocument.Parse(internalAsset.DefaultEventsConfiguration, options) : null;
            DefaultTopic = internalAsset.DefaultTopic;
            Events = internalAsset.Events;
            Status = internalAsset.Status;
            ProvisioningState = internalAsset.ProvisioningState;

            Datasets = new();
            if (internalAsset.Datasets != null)
            {
                foreach (DatasetInternal dataset in internalAsset.Datasets)
                {
                    if (!string.IsNullOrEmpty(dataset.Name))
                    {
                        Datasets[dataset.Name] = new()
                        {
                            DataPoints = dataset.DataPoints,
                            DatasetConfiguration = null,
                            Name = dataset.Name,
                            Topic = dataset.Topic,
                        };
                    }
                    else
                    {
                        //TODO log error
                    }
                }
            }
        }
    }

    public record DatasetInternal
    {
        /// <summary>
        /// The name of the dataset.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Protocol-specific JSON string that describes configuration for the specific dataset.
        /// </summary>
        public string? DatasetConfiguration { get; init; }

        /// <summary>
        /// Object that describes the topic information.
        /// </summary>
        public Topic? Topic { get; init; }

        /// <summary>
        /// Array of data points that are part of the dataset. Each data point can have per-data point configuration.
        /// </summary>
        public DataPointInternal[]? DataPoints { get; init; }
    }

    public record DataPointInternal
    {
        /// <summary>
        /// The name of the data point.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// The address of the source of the data in the asset (e.g. URL) so that a client can access the data source on the asset.
        /// </summary>
        public string DataSource { get; init; }

        /// <summary>
        /// An indication of how the data point should be mapped to OpenTelemetry.
        /// </summary>
        public string? ObservabilityMode { get; init; }

        /// <summary>
        /// Protocol-specific configuration for the data point. For OPC UA, this could include configuration like, publishingInterval, samplingInterval, and queueSize.
        /// </summary>
        public string? DataPointConfiguration { get; init; }

        internal DataPointInternal(string name, string dataSource)
        {
            Name = name;
            DataSource = dataSource;
        }
    }

    public record EventInternal
    {
        /// <summary>
        /// The name of the event.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// The address of the notifier of the event in the asset (e.g. URL) so that a client can access the event on the asset.
        /// </summary>
        public string? EventNotifier { get; init; }

        /// <summary>
        /// An indication of how the event should be mapped to OpenTelemetry.
        /// </summary>
        public string? ObservabilityMode { get; init; }

        /// <summary>
        /// Protocol-specific configuration for the event. For OPC UA, this could include configuration like, publishingInterval, samplingInterval, and queueSize.
        /// </summary>
        public string? EventConfiguration { get; init; }

        /// <summary>
        /// Object that describes the topic information.
        /// </summary>
        public Topic? Topic { get; init; }
    }
}
