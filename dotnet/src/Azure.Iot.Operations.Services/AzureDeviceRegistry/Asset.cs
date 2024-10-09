﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    public record Asset
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
        public JsonDocument? DefaultDatasetsConfiguration { get; init; }

        /// <summary>
        /// Protocol-specific default configuration for all data sets. Each data set can have its own configuration that overrides the default settings here. This assumes that each asset instance has one protocol.
        /// </summary>
        public JsonDocument? DefaultEventsConfiguration { get; init; }

        /// <summary>
        /// Object that describes the topic information.
        /// </summary>
        public Topic? DefaultTopic { get; init; }

        /// <summary>
        /// Array of datasets that are part of the asset. Each dataset can have per-dataset configuration.
        /// </summary>
        public Dataset[]? Datasets { get; init; }

        /// <summary>
        /// Array of events that are part of the asset. Each event can reference an asset type capability and have per-event configuration.
        /// </summary>
        public Event[]? Events { get; init; }

        /// <summary>
        /// Read only object to reflect changes that have occurred on the Edge. Similar to Kubernetes status property for custom resources.
        /// </summary>
        public Status? Status { get; init; }

        /// <summary>
        /// Provisioning state of the resource.
        /// </summary>
        public string? ProvisioningState { get; init; }
    }

    public record Dataset
    {
        /// <summary>
        /// The name of the dataset.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// Protocol-specific JSON string that describes configuration for the specific dataset.
        /// </summary>
        public JsonDocument? DatasetConfiguration { get; init; }

        /// <summary>
        /// Object that describes the topic information.
        /// </summary>
        public Topic? Topic { get; init; }

        /// <summary>
        /// Array of data points that are part of the dataset. Each data point can have per-data point configuration.
        /// </summary>
        public DataPoint[]? DataPoints { get; init; }
    }

    public record DataPoint
    {
        /// <summary>
        /// The name of the data point.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// The address of the source of the data in the asset (e.g. URL) so that a client can access the data source on the asset.
        /// </summary>
        public string? DataSource { get; init; }

        /// <summary>
        /// An indication of how the data point should be mapped to OpenTelemetry.
        /// </summary>
        public string? ObservabilityMode { get; init; }

        /// <summary>
        /// Protocol-specific configuration for the data point. For OPC UA, this could include configuration like, publishingInterval, samplingInterval, and queueSize.
        /// </summary>
        public JsonDocument? DataPointConfiguration { get; init; }
    }

    public record Event
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
        public JsonDocument? EventConfiguration { get; init; }

        /// <summary>
        /// Object that describes the topic information.
        /// </summary>
        public Topic? Topic { get; init; }
    }

    public record Topic
    {
        /// <summary>
        /// The topic path for messages sent for the specific entry.
        /// </summary>
        public string? Path { get; init; }

        /// <summary>
        /// The topic retain attribute for the specific entry.
        /// </summary>
        public string? Retain { get; init; }
    }

    public record Status
    {
        /// <summary>
        /// Array object to transfer and persist errors that originate from the Edge.
        /// </summary>
        public StatusError[]? Errors { get; init; }

        /// <summary>
        /// A read only incremental counter indicating the number of times the configuration has been modified from the perspective of the current actual (Edge) state of the Asset. Edge would be the only writer of this value and would sync back up to the cloud. In steady state, this should equal version.
        /// </summary>
        public long? Version { get; init; }

        /// <summary>
        /// Array of data set statuses that describe the status of each dataset.
        /// </summary>
        public StatusDatasets[]? Datasets { get; init; }

        /// <summary>
        /// Array of event statuses that describe the status of each event.
        /// </summary>
        public StatusEvents[]? Events { get; init; }
    }

    public record StatusError
    {
        /// <summary>
        /// Error code for classification of errors (ex: 400, 404, 500, etc.).
        /// </summary>
        public int? Code { get; init; }

        /// <summary>
        /// Human readable helpful error message to provide additional context for error (ex: “capability Id 'foo' does not exist”).
        /// </summary>
        public string? Message { get; init; }
    }

    public record StatusDatasets
    {
        /// <summary>
        /// The name of the data set. Must be unique within the status.datasets array. This name is used to correlate between the spec and status data set information.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// Defines the message schema reference properties.
        /// </summary>
        public MessageSchemaReference? MessageSchemaReference { get; init; }
    }

    public record StatusEvents
    {
        /// <summary>
        /// The name of the event. Must be unique within the status.events array. This name is used to correlate between the spec and status event information.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// Defines the message schema reference properties.
        /// </summary>
        public MessageSchemaReference? MessageSchemaReference { get; init; }
    }

    public record MessageSchemaReference
    {
        /// <summary>
        /// The reference to the message schema registry namespace.
        /// </summary>
        public string? SchemaRegistryNamespace { get; init; }

        /// <summary>
        /// The reference to the message schema name.
        /// </summary>
        public string? SchemaName { get; init; }

        /// <summary>
        /// The reference to the message schema version.
        /// </summary>
        public string? SchemaVersion { get; init; }
    }
}
