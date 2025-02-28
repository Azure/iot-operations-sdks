// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;

namespace Azure.Iot.Operations.Services.PassiveReplicationSample
{
    internal static class MqttClientFactoryProvider
    {
        public static Func<IServiceProvider, MqttSessionClient> MqttClientFactory = service =>
        {
            IConfiguration config = service.GetService<IConfiguration>()!;
            bool mqttDiag = config!.GetValue<bool>("mqttDiag");
            MqttSessionClientOptions options = new MqttSessionClientOptions()
            {
                EnableMqttLogging = mqttDiag,
            };

            return new MqttSessionClient(options);
        };
    }
}