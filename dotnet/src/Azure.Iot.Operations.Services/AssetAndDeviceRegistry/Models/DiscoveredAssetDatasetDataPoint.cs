// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DiscoveredAssetDatasetDataPoint
{
    public string? DataPointConfiguration { get; set; }

    public required string DataSource { get; set; }

    public DateTime? LastUpdatedOn { get; set; }

    public required string? Name { get; set; }

    public string? TypeRef { get; set; }
}
