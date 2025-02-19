// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Events;

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
