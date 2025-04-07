// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DetectedAssetDatasetSchemaElementSchema
{
    public List<DetectedAssetDataPointSchemaElementSchema>? DataPoints { get; set; } = default;

    public string? DataSetConfiguration { get; set; } = default;

    public string? Name { get; set; } = default;

    public Topic? Topic { get; set; } = default;
}
