// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record UsernamePasswordCredentials
{
    public string? PasswordSecretName { get; set; } = default;

    public string? UsernameSecretName { get; set; } = default;
}
