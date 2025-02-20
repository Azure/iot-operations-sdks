// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class GenericMqttClientCredentialsProvider : IMqttClientCredentialsProvider
    {
        private readonly MQTTnet.IMqttClientCredentialsProvider _mqttNetCredentialsProvider;
        private readonly MQTTnet.IMqttClient _underlyingClient;

        public GenericMqttClientCredentialsProvider(MQTTnet.IMqttClientCredentialsProvider mqttNetCredentialsProvider, MQTTnet.IMqttClient underlyingClient)
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
