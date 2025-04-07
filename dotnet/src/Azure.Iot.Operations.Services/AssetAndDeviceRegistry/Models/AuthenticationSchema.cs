// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record AuthenticationSchema
{
    public MethodSchema? Method { get; set; } = default;

    public UsernamePasswordCredentialsSchema? UsernamePasswordCredentials { get; set; } = default;

    public X509credentialsSchema? X509credentials { get; set; } = default;
}
