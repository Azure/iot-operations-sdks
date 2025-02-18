// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System.Security.Cryptography.X509Certificates;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class MqttNetCertificateSelectionHandler
    {
        private Func<MqttClientCertificateSelectionEventArgs, X509Certificate> _genericNetFunc;

        public MqttNetCertificateSelectionHandler(Func<MqttClientCertificateSelectionEventArgs, X509Certificate> genericFunc)
        {
            _genericNetFunc = genericFunc;
        }

        public X509Certificate HandleCertificateSelection(MQTTnet.Client.MqttClientCertificateSelectionEventArgs args)
        {
            return _genericNetFunc.Invoke(new MqttClientCertificateSelectionEventArgs(args.TargetHost, args.LocalCertificates, args.RemoveCertificate, args.AcceptableIssuers, MqttNetConverter.ToGeneric(args.TcpOptions)));
        }
    }
}
