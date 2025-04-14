// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AssetEventSchemaElement
{
    public JsonDocument? EventConfiguration { get; set; } = default;

    public string? EventNotifier { get; set; } = default;

    public string? Name { get; set; } = default;

    public AssetEventObservabilityMode? ObservabilityMode { get; set; } = default;

    public Topic? Topic { get; set; } = default;
}
