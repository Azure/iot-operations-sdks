// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class MqttNetMqttExtendedAuthenticationExchangeHandler : MQTTnet.Client.IMqttExtendedAuthenticationExchangeHandler
    {
        private IMqttExtendedAuthenticationExchangeHandler _mqttNetHandler;

        public MqttNetMqttExtendedAuthenticationExchangeHandler(IMqttExtendedAuthenticationExchangeHandler mqttNetHandler)
        {
            _mqttNetHandler = mqttNetHandler;
        }

        public Task HandleRequestAsync(MQTTnet.Client.MqttExtendedAuthenticationExchangeContext context)
        {
            return _mqttNetHandler.HandleRequestAsync(MqttNetConverter.ToGeneric(context));
        }
    }
}
