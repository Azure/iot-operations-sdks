// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record MessageSchemaReference
{
    public string? SchemaName { get; set; } = default;

    public string? SchemaNamespace { get; set; } = default;

    public string? SchemaVersion { get; set; } = default;
}
