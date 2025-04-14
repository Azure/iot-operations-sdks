// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DetectedAssetDatasetSchemaElement
{
    public List<DetectedAssetDataPointSchemaElement>? DataPoints { get; set; }

    public string? DataSetConfiguration { get; set; }

    public required string Name { get; set; }

    public Topic? Topic { get; set; }
}
