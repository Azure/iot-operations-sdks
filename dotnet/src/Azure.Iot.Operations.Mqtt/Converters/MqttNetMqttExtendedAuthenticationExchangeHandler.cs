// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class MqttNetMqttExtendedAuthenticationExchangeHandler : MQTTnet.IMqttEnhancedAuthenticationHandler
    {
        private IMqttEnhancedAuthenticationHandler _mqttNetHandler;

        public MqttNetMqttExtendedAuthenticationExchangeHandler(IMqttEnhancedAuthenticationHandler mqttNetHandler)
        {
            _mqttNetHandler = mqttNetHandler;
        }

        public Task HandleEnhancedAuthenticationAsync(MQTTnet.MqttEnhancedAuthenticationEventArgs eventArgs)
        {
            return _mqttNetHandler.HandleEnhancedAuthenticationAsync(
                new MqttEnhancedAuthenticationEventArgs(
                eventArgs.AuthenticationData,
                eventArgs.AuthenticationMethod,
                (MqttAuthenticateReasonCode)((int)eventArgs.ReasonCode),
                eventArgs.ReasonString,
                MqttNetConverter.ToGeneric(eventArgs.UserProperties)));
        }
    }
}
