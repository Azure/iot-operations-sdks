// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record Authentication
{
    public Method? Method { get; set; }

    public UsernamePasswordCredentials? UsernamePasswordCredentials { get; set; }

    public X509Credentials? X509Credentials { get; set; }
}
