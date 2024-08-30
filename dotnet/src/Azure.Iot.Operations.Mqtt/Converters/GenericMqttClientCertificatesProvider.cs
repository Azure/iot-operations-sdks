using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class GenericMqttClientCertificatesProvider : IMqttClientCertificatesProvider
    {
        MQTTnet.Client.IMqttClientCertificatesProvider _mqttNetCertificatesProvider;

        internal GenericMqttClientCertificatesProvider(MQTTnet.Client.IMqttClientCertificatesProvider mqttNetCertificatesProvider)
        {
            _mqttNetCertificatesProvider = mqttNetCertificatesProvider;
        }

        public X509CertificateCollection GetCertificates() => _mqttNetCertificatesProvider.GetCertificates();
    }
}
