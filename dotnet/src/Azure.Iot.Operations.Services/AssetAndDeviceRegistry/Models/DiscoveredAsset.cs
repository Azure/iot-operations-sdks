// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DiscoveredAsset
{
    /// <summary>
    /// URIs or type definition IDs for the asset type.
    /// </summary>
    public List<string>? AssetTypeRefs { get; set; } = default;

    /// <summary>
    /// A set of key-value pairs that contain custom attributes.
    /// </summary>
    public Dictionary<string, string>? Attributes { get; set; } = default;

    /// <summary>
    /// Array of datasets that are part of the asset. Each data set spec describes the data points that make up the set.
    /// </summary>
    public List<DiscoveredAssetDataset>? Datasets { get; set; } = default;

    /// <summary>
    /// Stringified JSON that contains connector-specific default configuration for all datasets. Each dataset can have its own configuration that overrides the default settings here.
    /// </summary>
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
    /// Human-readable display name.
    /// </summary>
    public string? DisplayName { get; set; } = default;

    /// <summary>
    /// Asset documentation reference.
    /// </summary>
    public string? DocumentationUri { get; set; } = default;

    /// <summary>
    /// Array of event groups that are part of the asset. Each event group can have per-event group configuration.
    /// </summary>
    public List<DiscoveredAssetEventGroup>? EventGroups { get; set; } = default;

    /// <summary>
    /// Asset ID provided by the customer.
    /// </summary>
    public string? ExternalAssetId { get; set; } = default;

    /// <summary>
    /// Asset hardware revision number.
    /// </summary>
    public string? HardwareRevision { get; set; } = default;

    /// <summary>
    /// Array of management groups that are part of the asset. Each management group can have a per-group configuration.
    /// </summary>
    public List<DiscoveredAssetManagementGroup>? ManagementGroups { get; set; } = default;

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
    public string? SoftwareRevision { get; set; } = default;

    /// <summary>
    /// Array of streams that are part of the asset. Each stream can have per-stream configuration.
    /// </summary>
    public List<DiscoveredAssetStream>? Streams { get; set; } = default;
}

public class DiscoveredAssetStream
{
    /// <summary>
    /// Destinations for a stream.
    /// </summary>
    public List<EventStreamDestination>? Destinations { get; set; } = default;

    /// <summary>
    /// Timestamp (in UTC) indicating when the stream was added or modified.
    /// </summary>
    public DateTime? LastUpdatedOn { get; set; } = default;

    /// <summary>
    /// Name of the stream definition.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Stringified JSON that contains connector-specific configuration that describes configuration for the specific stream.
    /// </summary>
    public string? StreamConfiguration { get; set; } = default;

    /// <summary>
    /// URI or type definition ID.
    /// </summary>
    public string? TypeRef { get; set; } = default;
}

public class DiscoveredAssetManagementGroup
{
    /// <summary>
    /// Array of actions that are part of the management group. Each action can have an individual configuration.
    /// </summary>
    public List<DiscoveredAssetManagementGroupAction>? Actions { get; set; } = default;

    /// <summary>
    /// Reference to a data source for a given management group.
    /// </summary>
    public string? DataSource { get; set; } = default;

    /// <summary>
    /// Default response timeout for all actions that are part of the management group.
    /// </summary>
    public ulong? DefaultTimeoutInSeconds { get; set; } = default;

    /// <summary>
    /// Default MQTT topic path on which a client will receive the request for all actions that are part of the management group.
    /// </summary>
    public string? DefaultTopic { get; set; } = default;

    /// <summary>
    /// Timestamp (in UTC) indicating when the management group was added or modified.
    /// </summary>
    public DateTime? LastUpdatedOn { get; set; } = default;

    /// <summary>
    /// Stringified JSON that contains connector-specific configuration for the management group.
    /// </summary>
    public string? ManagementGroupConfiguration { get; set; } = default;

    /// <summary>
    /// Name of the management group.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// URI or type definition ID.
    /// </summary>
    public string? TypeRef { get; set; } = default;
}

public class DiscoveredAssetManagementGroupAction
{
    /// <summary>
    /// Configuration for the action.
    /// </summary>
    public string? ActionConfiguration { get; set; } = default;

    /// <summary>
    /// Type of the action.
    /// </summary>
    public AssetManagementGroupActionType ActionType { get; set; } = default!;

    /// <summary>
    /// Timestamp (in UTC) indicating when the management action was added or modified.
    /// </summary>
    public DateTime? LastUpdatedOn { get; set; } = default;

    /// <summary>
    /// Name of the action.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The target URI on which a client can invoke the specific action.
    /// </summary>
    public string TargetUri { get; set; } = default!;

    /// <summary>
    /// Response timeout for the action.
    /// </summary>
    public ulong? TimeoutInSeconds { get; set; } = default;

    /// <summary>
    /// The MQTT topic path on which a client will receive the request for the action.
    /// </summary>
    public string? Topic { get; set; } = default;

    /// <summary>
    /// URI or type definition ID.
    /// </summary>
    public string? TypeRef { get; set; } = default;
}
