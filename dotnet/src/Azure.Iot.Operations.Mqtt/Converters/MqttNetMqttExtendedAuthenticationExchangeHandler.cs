using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
