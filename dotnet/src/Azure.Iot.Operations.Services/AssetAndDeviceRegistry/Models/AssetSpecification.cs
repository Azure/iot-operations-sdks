// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetSpecification
{
    public List<string>? AssetTypeRefs { get; set; }

    public Dictionary<string, string>? Attributes { get; set; }

    public List<AssetDatasetSchemaElement>? Datasets { get; set; }

    public string? DefaultDatasetsConfiguration { get; set; }

    public List<DefaultDatasetsDestinationsSchemaElement>? DefaultDatasetsDestinations { get; set; }

    public string? DefaultEventsConfiguration { get; set; }

    public string? Description { get; set; }

    public required DeviceRef DeviceRef { get; set; }

    public List<string>? DiscoveredAssetRefs { get; set; }

    public string? DisplayName { get; set; }

    public string? DocumentationUri { get; set; }

    public bool? Enabled { get; set; }

    public List<AssetEventSchemaElement>? Events { get; set; }

    public string? ExternalAssetId { get; set; }

    public string? HardwareRevision { get; set; }

    public string? Manufacturer { get; set; }

    public string? ManufacturerUri { get; set; }

    public string? Model { get; set; }

    public string? ProductCode { get; set; }

    public string? SerialNumber { get; set; }

    public string? SoftwareRevision { get; set; }

    public string? Uuid { get; set; }

    public ulong? Version { get; set; }
}
