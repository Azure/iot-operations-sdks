// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public class AssetStream
{
    public List<EventStreamDestination>? Destinations { get; set; } = default;

    public string Name { get; set; } = default!;

    public string? StreamConfiguration { get; set; } = default;

    public string? TypeRef { get; set; } = default;
}
