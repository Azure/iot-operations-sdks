// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System.Security.Cryptography.X509Certificates;

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
