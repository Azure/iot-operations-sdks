// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class MqttNetMqttClientCredentialsProvider : MQTTnet.Client.IMqttClientCredentialsProvider
    {
        private IMqttClientCredentialsProvider _mqttNetCredentialsProvider;
        private MQTTnet.Client.IMqttClient _underlyingClient;

        public MqttNetMqttClientCredentialsProvider(IMqttClientCredentialsProvider mqttNetCredentialsProvider, MQTTnet.Client.IMqttClient underlyingClient)
        {
            _mqttNetCredentialsProvider = mqttNetCredentialsProvider;
            _underlyingClient = underlyingClient;
        }

        public byte[]? GetPassword(MQTTnet.Client.MqttClientOptions clientOptions)
        {
            return _mqttNetCredentialsProvider.GetPassword(MqttNetConverter.ToGeneric(clientOptions, _underlyingClient));
        }

        public string GetUserName(MQTTnet.Client.MqttClientOptions clientOptions)
        {
            return _mqttNetCredentialsProvider.GetUserName(MqttNetConverter.ToGeneric(clientOptions, _underlyingClient));
        }
    }
}
