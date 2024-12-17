// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class MqttNetMqttClientCertificatesProvider : MQTTnet.Client.IMqttClientCertificatesProvider
    {
        IMqttClientCertificatesProvider _genericCertificatesProvider;

        internal MqttNetMqttClientCertificatesProvider(IMqttClientCertificatesProvider mqttNetCertificatesProvider)
        {
            _genericCertificatesProvider = mqttNetCertificatesProvider;
        }

        public X509CertificateCollection GetCertificates() => _genericCertificatesProvider.GetCertificates();
    }
}
