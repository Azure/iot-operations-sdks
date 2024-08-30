using System;
using System.Security.Cryptography.X509Certificates;

namespace Azure.Iot.Operations.Protocol.Models
{
    public sealed class MqttClientCertificateSelectionEventArgs : EventArgs
    {
        public MqttClientCertificateSelectionEventArgs(
            string targetHost,
            X509CertificateCollection localCertificates,
            X509Certificate remoteCertificate,
            string[] acceptableIssuers,
            MqttClientTcpOptions tcpOptions)
        {
            TargetHost = targetHost;
            LocalCertificates = localCertificates;
            RemoteCertificate = remoteCertificate;
            AcceptableIssuers = acceptableIssuers;
            TcpOptions = tcpOptions ?? throw new ArgumentNullException(nameof(tcpOptions));
        }

        public string[] AcceptableIssuers { get; }

        public X509CertificateCollection LocalCertificates { get; }

        public X509Certificate RemoteCertificate { get; }

        public string TargetHost { get; }

        public MqttClientTcpOptions TcpOptions { get; }
    }
}
