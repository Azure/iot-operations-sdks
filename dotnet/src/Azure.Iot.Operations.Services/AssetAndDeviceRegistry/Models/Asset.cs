// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record Asset
{
    /// <summary>
    /// URIs or type definition IDs.
    /// </summary>
    public List<string>? AssetTypeRefs { get; set; } = default;

    /// <summary>
    /// A set of key-value pairs that contain custom attributes set by the customer.
    /// </summary>
    public Dictionary<string, string>? Attributes { get; set; } = default;

    /// <summary>
    /// Array of data sets that are part of the asset. Each data set describes the data points that make up the set.
    /// </summary>
    public List<AssetDataset>? Datasets { get; set; } = default;

    /// <summary>
    /// Stringified JSON that contains connector-specific default configuration for all datasets. Each dataset can have its own configuration that overrides the default settings here.
    /// </summary>
    [JsonPropertyName("defaultDatasetsConfiguration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? DefaultDatasetsConfiguration { get; set; } = default;

    /// <summary>
    /// Default destinations for a dataset.
    /// </summary>
    public List<DatasetDestination>? DefaultDatasetsDestinations { get; set; } = default;

    /// <summary>
    /// Stringified JSON that contains connector-specific default configuration for all events. Each event can have its own configuration that overrides the default settings here.
    /// </summary>
    public string? DefaultEventsConfiguration { get; set; } = default;

    /// <summary>
    /// Default destinations for an event.
    /// </summary>
    public List<EventStreamDestination>? DefaultEventsDestinations { get; set; } = default;

    /// <summary>
    /// Stringified JSON that contains connector-specific default configuration for all management groups. Each management group can have its own configuration that overrides the default settings here.
    /// </summary>
    public string? DefaultManagementGroupsConfiguration { get; set; } = default;

    /// <summary>
    /// Stringified JSON that contains connector-specific default configuration for all streams. Each stream can have its own configuration that overrides the default settings here.
    /// </summary>
    public string? DefaultStreamsConfiguration { get; set; } = default;

    /// <summary>
    /// Default destinations for a stream.
    /// </summary>
    public List<EventStreamDestination>? DefaultStreamsDestinations { get; set; } = default;

    /// <summary>
    /// Human-readable description of the asset.
    /// </summary>
    public string? Description { get; set; } = default;

    /// <summary>
    /// Reference to the device that provides data for this asset. Must provide device name & endpoint on the device to use.
    /// </summary>
    public AssetDeviceRef DeviceRef { get; set; } = default!;

    /// <summary>
    /// Reference to a list of discovered assets. Populated only if the asset has been created from discovery flow. Discovered asset names must be provided.
    /// </summary>
    public List<string>? DiscoveredAssetRefs { get; set; } = default;

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string? DisplayName { get; set; } = default;

    /// <summary>
    /// Asset documentation reference.
    /// </summary>
    public string? DocumentationUri { get; set; } = default;

    /// <summary>
    /// Enabled/Disabled status of the asset.
    /// </summary>
    public bool? Enabled { get; set; } = default;

    /// <summary>
    /// Array of events groups that are part of the asset. Each event group can have per-event group configuration.
    /// </summary>
    public List<AssetEventGroup>? EventGroups { get; set; } = default;

    /// <summary>
    /// Asset ID provided by the customer.
    /// </summary>
    public string? ExternalAssetId { get; set; } = default;

    /// <summary>
    /// Asset hardware revision number.
    /// </summary>
    public string? HardwareRevision { get; set; } = default;

    /// <summary>
    /// A timestamp (in UTC) that is updated each time the resource is modified.
    /// </summary>
    public DateTime? LastTransitionTime { get; set; } = default;

    /// <summary>
    /// Array of management groups that are part of the asset.
    /// </summary>
    public List<AssetManagementGroup>? ManagementGroups { get; set; } = default;

    /// <summary>
    /// Asset manufacturer.
    /// </summary>
    public string? Manufacturer { get; set; } = default;

    /// <summary>
    /// Asset manufacturer URI.
    /// </summary>
    public string? ManufacturerUri { get; set; } = default;

    /// <summary>
    /// Asset model.
    /// </summary>
    public string? Model { get; set; } = default;

    /// <summary>
    /// Asset product code.
    /// </summary>
    public string? ProductCode { get; set; } = default;

    /// <summary>
    /// Asset serial number.
    /// </summary>
    public string? SerialNumber { get; set; } = default;

    /// <summary>
    /// Asset software revision number.
    /// </summary>
    [JsonPropertyName("softwareRevision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? SoftwareRevision { get; set; } = default;

    /// <summary>
    /// Array of streams that are part of the asset. Each stream can have per-stream configuration.
    /// </summary>
    public List<AssetStream>? Streams { get; set; } = default;

    /// <summary>
    /// Globally unique, immutable, non-reusable id.
    /// </summary>
    public string? Uuid { get; set; } = default;

    /// <summary>
    /// A read-only integer that is incremented each time the resource is modified the cloud.
    /// </summary>
    public ulong? Version { get; set; } = default;
}
