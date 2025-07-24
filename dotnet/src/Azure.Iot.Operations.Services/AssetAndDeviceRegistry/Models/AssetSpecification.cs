// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetSpecification
{
    public List<string>? AssetTypeRefs { get; set; }

    public Dictionary<string, string>? Attributes { get; set; }

    public List<AssetDataset>? Datasets { get; set; }

    public string? DefaultDatasetsConfiguration { get; set; }

    public List<DatasetDestination>? DefaultDatasetsDestinations { get; set; }

    public string? DefaultEventsConfiguration { get; set; }

    public List<EventStreamDestination>? DefaultEventsDestinations { get; set; }

    public string? DefaultManagementGroupsConfiguration { get; set; }

    public string? DefaultStreamsConfiguration { get; set; }

    public List<EventStreamDestination>? DefaultStreamsDestinations { get; set; }

    public string? Description { get; set; }

    public required AssetDeviceRef DeviceRef { get; set; }

    public List<string>? DiscoveredAssetRefs { get; set; }

    public string? DisplayName { get; set; }

    public string? DocumentationUri { get; set; }

    public bool? Enabled { get; set; }

    public List<AssetEvent>? Events { get; set; }

    public string? ExternalAssetId { get; set; }

    public string? HardwareRevision { get; set; }

    public DateTime? LastTransitionTime { get; set; }

    public List<AssetManagementGroup>? ManagementGroups { get; set; }

    public string? Manufacturer { get; set; }

    public string? ManufacturerUri { get; set; }

    public string? Model { get; set; }

    public string? ProductCode { get; set; }

    public string? SerialNumber { get; set; }

    public string? SoftwareRevision { get; set; }

    public List<AssetStream>? Streams { get; set; }
    public string? Uuid { get; set; }

    public ulong? Version { get; set; }
}
