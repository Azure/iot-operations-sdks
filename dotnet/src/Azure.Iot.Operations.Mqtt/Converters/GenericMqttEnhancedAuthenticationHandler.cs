// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Azure.Iot.Operations.Protocol.Models;
using MQTTnet;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class GenericMqttEnhancedAuthenticationHandler : Protocol.Models.IMqttEnhancedAuthenticationHandler
    {
        private MQTTnet.IMqttEnhancedAuthenticationHandler _mqttNetHandler;
        private MQTTnet.IMqttClient _underlyingClient;

        public GenericMqttEnhancedAuthenticationHandler(MQTTnet.IMqttEnhancedAuthenticationHandler mqttNetHandler, MQTTnet.IMqttClient underlyingClient)
        {
            _mqttNetHandler = mqttNetHandler;
            _underlyingClient = underlyingClient;
        }

        public Task HandleEnhancedAuthenticationAsync(Protocol.Models.MqttEnhancedAuthenticationEventArgs eventArgs)
        {
            // TODO wat do
            return _mqttNetHandler.HandleEnhancedAuthenticationAsync(new MQTTnet.MqttEnhancedAuthenticationEventArgs(null, null, default));
        }
    }
}
