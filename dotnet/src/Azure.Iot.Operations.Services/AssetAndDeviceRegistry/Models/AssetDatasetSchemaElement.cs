// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetDatasetSchemaElement
{
    public List<AssetDatasetDataPointSchemaElement>? DataPoints { get; set; }

    public string? DataSource { get; set; }

    public List<AssetDatasetDestinationSchemaElement>? Destinations { get; set; }

    public string? Name { get; set; }

    public string? TypeRef { get; set; }
}
