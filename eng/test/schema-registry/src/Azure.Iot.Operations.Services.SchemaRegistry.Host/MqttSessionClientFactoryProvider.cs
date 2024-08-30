using Azure.Iot.Operations.Mqtt.Session;
using System.Diagnostics;

namespace Azure.Iot.Operations.Services.SchemaRegistry.Host;

internal class MqttSessionClientFactoryProvider
{
    public static Func<IServiceProvider, MqttSessionClient> MqttClientFactory = service =>
    {
        IConfiguration? config = service.GetService<IConfiguration>();
        ILogger<MqttSessionClientFactoryProvider>? logger = service.GetService<ILogger<MqttSessionClientFactoryProvider>>();
        logger?.LogInformation("Creating SessionClient from Factory");
        bool mqttDiag = config!.GetValue<bool>("mqttDiag");
        if (mqttDiag) Trace.Listeners.Add(new ConsoleTraceListener());
        MqttSessionClient client =  new (new MqttSessionClientOptions { EnableMqttLogging = mqttDiag, RetryOnFirstConnect = true });
        client.SessionLostAsync += e =>
        {
            logger?.LogError("Could not recover connection to MQTT broker. {r}", e.Reason);
            return Task.CompletedTask;
        };
        return client;
    };

}
