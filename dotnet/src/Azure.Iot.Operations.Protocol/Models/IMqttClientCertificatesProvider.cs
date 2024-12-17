using System.Security.Cryptography.X509Certificates;

namespace Azure.Iot.Operations.Protocol.Models
{
    public interface IMqttClientCertificatesProvider
    {
        X509CertificateCollection GetCertificates();
    }
}
