// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace Azure.Iot.Operations.Protocol.Connection
{
    internal sealed class X509ClientCertificateLocator
    {
        internal static X509Certificate2 Load(string certFile, string keyFile, string? keyFilePassword)
        {
            X509Certificate2? cert = string.IsNullOrEmpty(keyFilePassword) ?
                X509Certificate2.CreateFromPemFile(certFile, keyFile) :
                X509Certificate2.CreateFromEncryptedPemFile(certFile, keyFilePassword, keyFile);

            if (cert.NotAfter.ToUniversalTime() < DateTime.UtcNow)
            {
                throw new ArgumentException($"Certificate has expired");
            }

            return cert;
        }
    }
}
