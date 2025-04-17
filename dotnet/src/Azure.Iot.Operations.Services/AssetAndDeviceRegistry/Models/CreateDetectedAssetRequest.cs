// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record CreateDetectedAssetRequest
{
    public required string AssetEndpointProfileRef { get; set; }

    public string? AssetName { get; set; }

    public List<DetectedAssetDatasetSchemaElement>? Datasets { get; set; }

    public string? DefaultDatasetsConfiguration { get; set; }

    public string? DefaultEventsConfiguration { get; set; }

    public Topic? DefaultTopic { get; set; }

    public string? DocumentationUri { get; set; }

    public List<DetectedAssetEventSchemaElement>? Events { get; set; }

    public string? HardwareRevision { get; set; }

    public string? Manufacturer { get; set; }

    public string? ManufacturerUri { get; set; }

    public string? Model { get; set; }

    public string? ProductCode { get; set; }

    public string? SerialNumber { get; set; }

    public string? SoftwareRevision { get; set; }
}
