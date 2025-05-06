// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record DeviceConfigStatus
{
    public ConfigError? Error { get; set; }

    public string? LastTransitionTime { get; set; }

    public ulong? Version { get; set; }
}
