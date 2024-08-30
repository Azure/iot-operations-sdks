using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Mqtt.Session;
using MQTTnet;
using MQTTnet.Client;
using System.Diagnostics;

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