// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class GenericMqttExtendedAuthenticationExchangeHandler : IMqttEnhancedAuthenticationHandler
    {
        private MQTTnet.IMqttEnhancedAuthenticationHandler _mqttNetHandler;
        private MQTTnet.IMqttClient _underlyingClient;

        public GenericMqttExtendedAuthenticationExchangeHandler(MQTTnet.IMqttEnhancedAuthenticationHandler mqttNetHandler, MQTTnet.IMqttClient underlyingClient)
        {
            _mqttNetHandler = mqttNetHandler;
            _underlyingClient = underlyingClient;
        }

        public Task HandleEnhancedAuthenticationAsync(MqttEnhancedAuthenticationEventArgs eventArgs)
        {
            return _mqttNetHandler.HandleRequestAsync(MqttNetConverter.FromGeneric(eventArgs, _underlyingClient));
        }
    }
}
