// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class MqttNetMqttEnhancedAuthenticationHandler : MQTTnet.IMqttEnhancedAuthenticationHandler
    {
        private readonly IMqttEnhancedAuthenticationHandler _mqttNetHandler;

        public MqttNetMqttEnhancedAuthenticationHandler(IMqttEnhancedAuthenticationHandler mqttNetHandler)
        {
            _mqttNetHandler = mqttNetHandler;
        }

        public Task HandleEnhancedAuthenticationAsync(MQTTnet.MqttEnhancedAuthenticationEventArgs eventArgs)
        {
            return _mqttNetHandler.HandleEnhancedAuthenticationAsync(
                new(
                    eventArgs.AuthenticationData,
                    eventArgs.AuthenticationMethod,
                    (MqttAuthenticateReasonCode)((int) eventArgs.ReasonCode),
                    eventArgs.ReasonString,
                    MqttNetConverter.ToGeneric(eventArgs.UserProperties)));
        }
    }
}
