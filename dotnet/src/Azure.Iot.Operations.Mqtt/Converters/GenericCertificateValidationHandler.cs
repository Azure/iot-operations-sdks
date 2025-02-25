// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class GenericCertificateValidationHandler
    {
        private readonly Func<MQTTnet.MqttClientCertificateValidationEventArgs, bool> _mqttNetFunc;

        public GenericCertificateValidationHandler(Func<MQTTnet.MqttClientCertificateValidationEventArgs, bool> mqttNetFunc)
        {
            _mqttNetFunc = mqttNetFunc;
        }

        public bool HandleCertificateValidation(MqttClientCertificateValidationEventArgs args)
        {
            return _mqttNetFunc.Invoke(new MQTTnet.MqttClientCertificateValidationEventArgs(args.Certificate, args.Chain, args.SslPolicyErrors, MqttNetConverter.FromGeneric(args.ClientOptions)));
        }
    }
}
