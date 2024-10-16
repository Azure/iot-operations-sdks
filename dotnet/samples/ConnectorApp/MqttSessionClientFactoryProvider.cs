using Azure.Iot.Operations.Mqtt.Session;
using System.Diagnostics;

namespace Azure.Iot.Operations.ConnectorSample;

internal static class MqttSessionClientFactoryProvider
{
    public static Func<IServiceProvider, MqttSessionClient> MqttSessionClientFactory = service =>
    {
        IConfiguration? config = service.GetService<IConfiguration>();
        bool mqttDiag = config!.GetValue<bool>("mqttDiag");
        MqttSessionClientOptions sessionClientOptions = new()
        {
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
