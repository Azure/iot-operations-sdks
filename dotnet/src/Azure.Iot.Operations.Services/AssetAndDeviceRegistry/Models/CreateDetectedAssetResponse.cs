// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record CreateDetectedAssetResponse
{
    public required string Name { get; set; }

    public required DiscoveredAssetResponseSpecification Specification { get; set; }
}

public record DiscoveredAssetResponseSpecification
{
        public List<string>? AssetTypeRefs { get; set; }

        public Dictionary<string, string>? Attributes { get; set; }

        public List<DiscoveredAssetDataset>? Datasets { get; set; }

        public JsonDocument? DefaultDatasetsConfiguration { get; set; }

        public List<DatasetDestination>? DefaultDatasetsDestinations { get; set; }

        public JsonDocument? DefaultEventsConfiguration { get; set; }

        public List<EventStreamDestination>? DefaultEventsDestinations { get; set; }

        public JsonDocument? DefaultManagementGroupsConfiguration { get; set; }

        public JsonDocument? DefaultStreamsConfiguration { get; set; }

        public List<EventStreamDestination>? DefaultStreamsDestinations { get; set; }

        public required AssetDeviceRef DeviceRef { get; set; }

        public required string DiscoveryId { get; set; }

        public string? DocumentationUri { get; set; }

        public List<DiscoveredAssetEvent>? Events { get; set; }

        public string? HardwareRevision { get; set; }

        public List<DiscoveredAssetManagementGroup>? ManagementGroups { get; set; }

        public string? Manufacturer { get; set; }

        public string? ManufacturerUri { get; set; }

        public string? Model { get; set; }

        public string? ProductCode { get; set; }

        public string? SerialNumber { get; set; }

        public string? SoftwareRevision { get; set; }

        public List<DiscoveredAssetStream>? Streams { get; set; }

        public ulong Version { get; set; }
}

public record DiscoveredAssetEvent
{
    public List<DiscoveredAssetEventDataPoint>? DataPoints { get; set; }

    public List<EventStreamDestination>? Destinations { get; set; }

    public JsonDocument? EventConfiguration { get; set; }

    public required string EventNotifier { get; set; }

    public DateTime? LastUpdatedOn { get; set; }

    public required string Name { get; set; }

    public string? TypeRef { get; set; }
}
