﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetEventSchemaElementSchema
{
    public string? EventConfiguration { get; set; } = default;

    public string? EventNotifier { get; set; } = default;

    public string? Name { get; set; } = default;

    public AssetEventObservabilityModeSchema? ObservabilityMode { get; set; } = default;

    public Topic? Topic { get; set; } = default;
}
