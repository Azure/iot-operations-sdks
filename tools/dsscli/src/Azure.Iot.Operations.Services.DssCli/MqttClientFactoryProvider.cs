using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Mqtt;
using MQTTnet;
using MQTTnet.Client;
using System.Diagnostics;

internal static class MqttClientFactoryProvider
{

    public static Func<IServiceProvider, StateStoreClient> StateStoreClientFactory = service => new StateStoreClient(service.GetRequiredService<ApplicationContext>(), service.GetService<OrderedAckMqttClient>()!);

    public static Func<IServiceProvider, OrderedAckMqttClient> MqttClientFactory = service =>
    {
        IConfiguration config = service.GetService<IConfiguration>()!;
        bool mqttDiag = config!.GetValue<bool>("mqttDiag");
        MQTTnet.Client.IMqttClient result;
        if (mqttDiag)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
            result = new MqttFactory().CreateMqttClient(MqttNetTraceLogger.CreateTraceLogger());
        }
        else
        {
            result = new MqttFactory().CreateMqttClient();
        }
        return new OrderedAckMqttClient(result);
    };
}
