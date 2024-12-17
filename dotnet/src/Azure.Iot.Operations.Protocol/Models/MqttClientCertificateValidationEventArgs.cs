// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Azure.Iot.Operations.Protocol.Models
{
    public sealed class MqttClientCertificateValidationEventArgs(X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors, IMqttClientChannelOptions clientOptions) : EventArgs
    {
        public X509Certificate Certificate { get; } = certificate;

        public X509Chain Chain { get; } = chain;

        public IMqttClientChannelOptions ClientOptions { get; } = clientOptions ?? throw new ArgumentNullException(nameof(clientOptions));

        public SslPolicyErrors SslPolicyErrors { get; } = sslPolicyErrors;
    }
}
