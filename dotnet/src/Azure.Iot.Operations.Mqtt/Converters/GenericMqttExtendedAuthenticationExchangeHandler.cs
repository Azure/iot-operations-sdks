// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class GenericMqttExtendedAuthenticationExchangeHandler : IMqttExtendedAuthenticationExchangeHandler
    {
        private MQTTnet.Client.IMqttExtendedAuthenticationExchangeHandler _mqttNetHandler;
        private MQTTnet.Client.IMqttClient _underlyingClient;

        public GenericMqttExtendedAuthenticationExchangeHandler(MQTTnet.Client.IMqttExtendedAuthenticationExchangeHandler mqttNetHandler, MQTTnet.Client.IMqttClient underlyingClient)
        {
            _mqttNetHandler = mqttNetHandler;
            _underlyingClient = underlyingClient;
        }

        public Task HandleRequestAsync(MqttExtendedAuthenticationExchangeContext context)
        {
            return _mqttNetHandler.HandleRequestAsync(MqttNetConverter.FromGeneric(context, _underlyingClient));
        }
    }
}
