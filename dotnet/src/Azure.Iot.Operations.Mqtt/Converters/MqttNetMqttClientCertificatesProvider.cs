// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System.Security.Cryptography.X509Certificates;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class MqttNetMqttClientCertificatesProvider : MQTTnet.IMqttClientCertificatesProvider
    {
        private readonly IMqttClientCertificatesProvider _genericCertificatesProvider;

        internal MqttNetMqttClientCertificatesProvider(IMqttClientCertificatesProvider mqttNetCertificatesProvider)
        {
            _genericCertificatesProvider = mqttNetCertificatesProvider;
        }

        public X509CertificateCollection GetCertificates() => _genericCertificatesProvider.GetCertificates();
    }
}
