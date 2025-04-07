// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetDatasetSchemaElement
{
    public List<AssetDataPointSchemaElement>? DataPoints { get; set; } = default;

    public string? DatasetConfiguration { get; set; } = default;

    public string? Name { get; set; } = default;

    public Topic? Topic { get; set; } = default;
}
