using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class GenericCertificateValidationHandler
    {
        private Func<MQTTnet.Client.MqttClientCertificateValidationEventArgs, bool> _mqttNetFunc;

        public GenericCertificateValidationHandler(Func<MQTTnet.Client.MqttClientCertificateValidationEventArgs, bool> mqttNetFunc)
        {
            _mqttNetFunc = mqttNetFunc;
        }

        public bool HandleCertificateValidation(MqttClientCertificateValidationEventArgs args)
        {
            return _mqttNetFunc.Invoke(new MQTTnet.Client.MqttClientCertificateValidationEventArgs(args.Certificate, args.Chain, args.SslPolicyErrors, MqttNetConverter.FromGeneric(args.ClientOptions)));
        }
    }
}
