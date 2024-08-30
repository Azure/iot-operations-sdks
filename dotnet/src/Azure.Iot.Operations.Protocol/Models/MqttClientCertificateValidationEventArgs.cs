using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Azure.Iot.Operations.Protocol.Models
{
    public sealed class MqttClientCertificateValidationEventArgs : EventArgs
    {
        public MqttClientCertificateValidationEventArgs(X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors, IMqttClientChannelOptions clientOptions)
        {
            Certificate = certificate;
            Chain = chain;
            SslPolicyErrors = sslPolicyErrors;
            ClientOptions = clientOptions ?? throw new ArgumentNullException(nameof(clientOptions));
        }

        public X509Certificate Certificate { get; }

        public X509Chain Chain { get; }

        public IMqttClientChannelOptions ClientOptions { get; }

        public SslPolicyErrors SslPolicyErrors { get; }
    }
}
