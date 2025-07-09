// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DiscoveredAssetDataset
{
    public List<DiscoveredAssetDatasetDataPoint>? DataPoints { get; set; }

    public string? DataSetConfiguration { get; set; }

    public string? DataSource { get; set; }

    public List<DatasetDestination>? Destinations { get; set; }

    public DateTime? LastUpdatedOn { get; set; }

    public required string Name { get; set; }

    public string? TypeRef { get; set; }
}
