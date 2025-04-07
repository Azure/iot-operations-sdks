// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record X509credentialsSchema
{
    public string? CertificateSecretName { get; set; } = default;
}
