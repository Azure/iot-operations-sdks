using System;
using System.Security.Cryptography.X509Certificates;

namespace Azure.Iot.Operations.Protocol.Models
{
    public sealed class MqttClientCertificateSelectionEventArgs(
        string targetHost,
        X509CertificateCollection localCertificates,
        X509Certificate remoteCertificate,
        string[] acceptableIssuers,
        MqttClientTcpOptions tcpOptions) : EventArgs
    {
        public string[] AcceptableIssuers { get; } = acceptableIssuers;

        public X509CertificateCollection LocalCertificates { get; } = localCertificates;

        public X509Certificate RemoteCertificate { get; } = remoteCertificate;

        public string TargetHost { get; } = targetHost;

        public MqttClientTcpOptions TcpOptions { get; } = tcpOptions ?? throw new ArgumentNullException(nameof(tcpOptions));
    }
}
