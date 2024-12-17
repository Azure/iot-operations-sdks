using Azure.Iot.Operations.Protocol.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class MqttNetHandler
    {
        private Func<MqttApplicationMessageReceivedEventArgs, Task> _genericNetFunc;

        public MqttNetHandler(Func<MqttApplicationMessageReceivedEventArgs, Task> genericFunc)
        {
            _genericNetFunc = genericFunc;
        }

        public Task Handle(MQTTnet.Client.MqttApplicationMessageReceivedEventArgs args)
        {
            var genericArgs = 
                MqttNetConverter.ToGeneric(
                    args,
                    async (genericArgs, cancellationToken) =>
                    {
                        await args.AcknowledgeAsync(cancellationToken);
                    });

            _genericNetFunc.Invoke(genericArgs);

            args.AutoAcknowledge = genericArgs.AutoAcknowledge;

            return Task.CompletedTask;
        }
    }
}
