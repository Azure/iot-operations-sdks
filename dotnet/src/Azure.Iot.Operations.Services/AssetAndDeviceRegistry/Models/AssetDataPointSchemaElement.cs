// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetDataPointSchemaElement
{
    public JsonDocument? DataPointConfiguration { get; set; }

    public string? DataSource { get; set; }

    public string? Name { get; set; }

    public AssetDataPointObservabilityMode? ObservabilityMode { get; set; }
}
