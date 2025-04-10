// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record CreateDetectedAssetRequest
{
    public string? AssetEndpointProfileRef { get; set; } = default;

    public string? AssetName { get; set; } = default;

    public List<DetectedAssetDatasetSchemaElement>? Datasets { get; set; } = default;

    public string? DefaultDatasetsConfiguration { get; set; } = default;

    public string? DefaultEventsConfiguration { get; set; } = default;

    public Topic? DefaultTopic { get; set; } = default;

    public string? DocumentationUri { get; set; } = default;

    public List<DetectedAssetEventSchemaElement>? Events { get; set; } = default;

    public string? HardwareRevision { get; set; } = default;

    public string? Manufacturer { get; set; } = default;

    public string? ManufacturerUri { get; set; } = default;

    public string? Model { get; set; } = default;

    public string? ProductCode { get; set; } = default;

    public string? SerialNumber { get; set; } = default;

    public string? SoftwareRevision { get; set; } = default;
}
