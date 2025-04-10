// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record Authentication
{
    public Method? Method { get; set; } = default;

    public UsernamePasswordCredentials? UsernamePasswordCredentials { get; set; } = default;

    public X509Credentials? X509Credentials { get; set; } = default;
}
