// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DetectedAssetDataPointSchemaElement
{
    public string? DataPointConfiguration { get; set; }

    public required string DataSource { get; set; }

    public string? LastUpdatedOn { get; set; }

    public string? Name { get; set; }
}
