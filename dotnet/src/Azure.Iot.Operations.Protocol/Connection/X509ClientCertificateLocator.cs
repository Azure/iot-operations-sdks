// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace Azure.Iot.Operations.Protocol.Connection
{
    internal class X509ClientCertificateLocator
    {
        internal static X509Certificate2 Load(string certFile, string keyFile, string? keyFilePassword)
        {
            X509Certificate2? cert = string.IsNullOrEmpty(keyFilePassword) ?
                X509Certificate2.CreateFromPemFile(certFile, keyFile) :
                X509Certificate2.CreateFromEncryptedPemFile(certFile, keyFilePassword, keyFile);

            if (cert.NotAfter.ToUniversalTime() < DateTime.UtcNow)
            {
                throw new ArgumentException($"Cert '{cert.Subject}' expired '{cert.GetExpirationDateString()}'");
            }

            Trace.TraceInformation($"Loaded Cert: {cert.SubjectName.Name} {cert.Thumbprint} issued by {cert.Issuer}, not after: {cert.GetExpirationDateString()}");

            // https://github.com/dotnet/runtime/issues/45680#issuecomment-739912495
            return new X509Certificate2(cert.Export(X509ContentType.Pkcs12)); ;
        }
    }
}
