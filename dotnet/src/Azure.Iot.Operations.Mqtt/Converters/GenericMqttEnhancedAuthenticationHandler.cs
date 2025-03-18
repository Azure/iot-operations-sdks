// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class GenericMqttExtendedAuthenticationExchangeHandler : IMqttEnhancedAuthenticationHandler
    {
        private readonly MQTTnet.IMqttEnhancedAuthenticationHandler _mqttNetHandler;
        private readonly MQTTnet.IMqttClient _underlyingClient;

        public GenericMqttExtendedAuthenticationExchangeHandler(MQTTnet.IMqttEnhancedAuthenticationHandler mqttNetHandler, MQTTnet.IMqttClient underlyingClient)
        {
            _mqttNetHandler = mqttNetHandler;
            _underlyingClient = underlyingClient;
        }

        public Task HandleEnhancedAuthenticationAsync(MqttEnhancedAuthenticationEventArgs eventArgs)
        {
            var hiddenField = typeof(MQTTnet.MqttClient).GetField("_adapter", BindingFlags.NonPublic | BindingFlags.Instance);
            MQTTnet.Adapter.IMqttChannelAdapter? channelAdapter = (MQTTnet.Adapter.IMqttChannelAdapter?)hiddenField!.GetValue(_underlyingClient);

            return _mqttNetHandler.HandleEnhancedAuthenticationAsync(
            new(
                new()
                {
                    AuthenticationData = eventArgs.AuthenticationData,
                    AuthenticationMethod = eventArgs.AuthenticationMethod,
                    ReasonCode = (MQTTnet.Protocol.MqttAuthenticateReasonCode)(int)eventArgs.ReasonCode,
                    ReasonString = eventArgs.ReasonString,
                    UserProperties = MqttNetConverter.FromGeneric(eventArgs.UserProperties)
                },

                channelAdapter,
                default));
        }
    }
}
