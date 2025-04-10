// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record Topic
{
    public string? Path { get; set; } = default;

    public Retain? Retain { get; set; } = default;
}
