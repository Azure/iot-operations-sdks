using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Mqtt;
using MQTTnet;
using System.Diagnostics;
using Azure.Iot.Operations.Protocol;

internal static class MqttClientFactoryProvider
{

    public static Func<IServiceProvider, StateStoreClient> StateStoreClientFactory = service => new StateStoreClient(service.GetRequiredService<ApplicationContext>(), service.GetService<OrderedAckMqttClient>()!);

    public static Func<IServiceProvider, OrderedAckMqttClient> MqttClientFactory = service =>
    {
        IConfiguration config = service.GetService<IConfiguration>()!;
        bool mqttDiag = config!.GetValue<bool>("mqttDiag");
        MQTTnet.IMqttClient result;
        if (mqttDiag)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            result = new MqttClientFactory().CreateMqttClient(MqttNetTraceLogger.CreateTraceLogger());
        }
        else
        {
            result = new MqttClientFactory().CreateMqttClient();
        }
        return new OrderedAckMqttClient(result);
    };
}
