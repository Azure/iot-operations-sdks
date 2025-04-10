// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DatasetsSchemaElement
{
    public MessageSchemaReference? MessageSchemaReference { get; set; } = default;

    public string? Name { get; set; } = default;
}
