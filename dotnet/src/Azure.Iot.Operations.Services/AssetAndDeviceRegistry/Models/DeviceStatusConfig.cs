// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceStatusConfig
{
    public ConfigError? Error { get; set; } = default;

    public string? LastTransitionTime { get; set; } = default;

    public ulong? Version { get; set; } = default;
}
