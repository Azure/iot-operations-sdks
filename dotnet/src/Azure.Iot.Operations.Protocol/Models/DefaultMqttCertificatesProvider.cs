using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace Azure.Iot.Operations.Protocol.Models
{
    public sealed class DefaultMqttCertificatesProvider : IMqttClientCertificatesProvider
    {
        readonly X509Certificate2Collection _certificates;

        public DefaultMqttCertificatesProvider(X509Certificate2Collection certificates)
        {
            _certificates = certificates;
        }

        public DefaultMqttCertificatesProvider(IEnumerable<X509Certificate> certificates)
        {
            _certificates = new X509Certificate2Collection();
            
            if (certificates != null)
            {
                foreach (var certificate in certificates)
                {
                    _certificates.Add(certificate);
                }
            }
        }

        public X509CertificateCollection GetCertificates()
        {
            return _certificates;
        }
    }
}
