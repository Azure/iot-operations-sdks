using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace Azure.Iot.Operations.Connector
{
    public static class MqttSessionClientFactoryProvider
    {
        public static Func<IServiceProvider, IMqttClient> MqttSessionClientFactory = service =>
        {
            IConfiguration? config = service.GetService<IConfiguration>();
            bool mqttDiag = config!.GetValue<bool>("mqttDiag");
            if (mqttDiag)
            {
                Trace.Listeners.Add(new ConsoleTraceListener());
            }

            MqttSessionClientOptions sessionClientOptions = new()
            {
                EnableMqttLogging = mqttDiag,
                RetryOnFirstConnect = true,
            };


            return new MqttSessionClient(sessionClientOptions);
        };
    }
}