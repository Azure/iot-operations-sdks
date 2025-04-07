// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetDataPointSchemaElementSchema
{
    public string? DataPointConfiguration { get; set; } = default;

    public string? DataSource { get; set; } = default;

    public string? Name { get; set; } = default;

    public AssetDataPointObservabilityModeSchema? ObservabilityMode { get; set; } = default;
}
