// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System.Security.Cryptography.X509Certificates;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class GenericCertificateSelectionHandler
    {
        private Func<MQTTnet.Client.MqttClientCertificateSelectionEventArgs, X509Certificate> _mqttNetFunc;

        public GenericCertificateSelectionHandler(Func<MQTTnet.Client.MqttClientCertificateSelectionEventArgs, X509Certificate> mqttNetFunc)
        {
            _mqttNetFunc = mqttNetFunc;
        }

        public X509Certificate HandleCertificateSelection(MqttClientCertificateSelectionEventArgs args)
        {
            return _mqttNetFunc.Invoke(new MQTTnet.Client.MqttClientCertificateSelectionEventArgs(args.TargetHost, args.LocalCertificates, args.RemoteCertificate, args.AcceptableIssuers, MqttNetConverter.FromGeneric(args.TcpOptions)));
        }
    }
}
