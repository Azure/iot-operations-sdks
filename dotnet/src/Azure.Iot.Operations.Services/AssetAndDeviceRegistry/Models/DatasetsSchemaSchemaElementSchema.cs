﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DatasetsSchemaSchemaElementSchema
{
    public MessageSchemaReference? MessageSchemaReference { get; set; } = default;

    public string? Name { get; set; } = default;
}
