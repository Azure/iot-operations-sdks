// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class MqttNetMqttClientCredentialsProvider : MQTTnet.IMqttClientCredentialsProvider
    {
        private readonly IMqttClientCredentialsProvider _mqttNetCredentialsProvider;
        private readonly MQTTnet.IMqttClient _underlyingClient;

        public MqttNetMqttClientCredentialsProvider(IMqttClientCredentialsProvider mqttNetCredentialsProvider, MQTTnet.IMqttClient underlyingClient)
        {
            _mqttNetCredentialsProvider = mqttNetCredentialsProvider;
            _underlyingClient = underlyingClient;
        }

        public byte[]? GetPassword(MQTTnet.MqttClientOptions clientOptions)
        {
            return _mqttNetCredentialsProvider.GetPassword(MqttNetConverter.ToGeneric(clientOptions, _underlyingClient));
        }

        public string GetUserName(MQTTnet.MqttClientOptions clientOptions)
        {
            return _mqttNetCredentialsProvider.GetUserName(MqttNetConverter.ToGeneric(clientOptions, _underlyingClient));
        }
    }
}
