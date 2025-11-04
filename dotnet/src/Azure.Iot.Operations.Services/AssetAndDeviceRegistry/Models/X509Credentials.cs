// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record X509Credentials
{
    public string? CertificateSecretName { get; set; }

    /// <summary>
    /// A reference to the secret containing the combined intermediate certificates in PEM format.
    /// </summary>
    public string? IntermediateCertificatesSecretName { get; set; } = default;

    /// <summary>
    /// A reference to the secret containing the certificate private key in PEM or DER format.
    /// </summary>
    public string? KeySecretName { get; set; } = default;
}
