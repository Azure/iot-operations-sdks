// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetDatasetDataPointSchemaElement
{
    public string? DataPointConfiguration { get; set; }

    public string? DataSource { get; set; }

    public string? Name { get; set; }

    public string? TypeRef { get; set; }
}
