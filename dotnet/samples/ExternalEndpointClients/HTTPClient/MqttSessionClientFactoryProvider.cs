using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Retry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace HTTPClient;

internal static class MqttSessionClientFactoryProvider
{
    public static Func<IServiceProvider, MqttSessionClient> MqttSessionClientFactory = service =>
    {
        IConfiguration? config = service.GetService<IConfiguration>();
        bool mqttDiag = config!.GetValue<bool>("mqttDiag");
        MqttSessionClientOptions sessionClientOptions = new()
        {
            ConnectionRetryPolicy = new ExponentialBackoffRetryPolicy(10, TimeSpan.FromSeconds(5)),
            EnableMqttLogging = mqttDiag,
            RetryOnFirstConnect = true,
        };

        if (mqttDiag)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
        }

        return new MqttSessionClient(sessionClientOptions);
    };
}
