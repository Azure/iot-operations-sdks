using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class GenericMqttClientCredentialsProvider : IMqttClientCredentialsProvider
    {
        private MQTTnet.Client.IMqttClientCredentialsProvider _mqttNetCredentialsProvider;
        private MQTTnet.Client.IMqttClient _underlyingClient;

        public GenericMqttClientCredentialsProvider(MQTTnet.Client.IMqttClientCredentialsProvider mqttNetCredentialsProvider, MQTTnet.Client.IMqttClient underlyingClient)
        {
            _mqttNetCredentialsProvider = mqttNetCredentialsProvider;
            _underlyingClient = underlyingClient;
        }

        public byte[] GetPassword(MqttClientOptions clientOptions)
        {
            return _mqttNetCredentialsProvider.GetPassword(MqttNetConverter.FromGeneric(clientOptions, _underlyingClient));
        }

        public string GetUserName(MqttClientOptions clientOptions)
        {
            return _mqttNetCredentialsProvider.GetUserName(MqttNetConverter.FromGeneric(clientOptions, _underlyingClient));
        }
    }
}
