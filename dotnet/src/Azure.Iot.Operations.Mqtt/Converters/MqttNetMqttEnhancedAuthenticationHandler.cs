// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class MqttNetMqttEnhancedAuthenticationHandler : MQTTnet.IMqttEnhancedAuthenticationHandler
    {
        private IMqttEnhancedAuthenticationHandler _mqttNetHandler;

        public MqttNetMqttEnhancedAuthenticationHandler(IMqttEnhancedAuthenticationHandler mqttNetHandler)
        {
            _mqttNetHandler = mqttNetHandler;
        }

        public Task HandleEnhancedAuthenticationAsync(MQTTnet.MqttEnhancedAuthenticationEventArgs eventArgs)
        {
            return _mqttNetHandler.HandleRequestAsync(MqttNetConverter.ToGeneric(context));
        }
    }
}
