using System.Diagnostics;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.RPC;
using SimpleRpcServer;

string commandName = "someCommandName";

// If you want to log the MQTT layer publishes, subscribes, connects, etc.
bool logMqtt = false;

if (logMqtt) Trace.Listeners.Add(new ConsoleTraceListener());
await using MqttSessionClient mqttClient = new(new MqttSessionClientOptions { EnableMqttLogging = false });
var mcs = new MqttConnectionSettings("127.0.0.1", Guid.NewGuid().ToString())
{
    TcpPort = 1883,
    UseTls = false
};
await mqttClient.ConnectAsync(mcs);
Console.WriteLine("Connected to broker");

await using SampleCommandExecutor rpcExecutor = new(new(), mqttClient, commandName, new Utf8JsonSerializer())
{
    OnCommandReceived = (request, cancellationToken) =>
    {
        CommandResponseMetadata responseMetadata = new CommandResponseMetadata();
        long stageFourTicks = DateTime.UtcNow.Ticks;
        responseMetadata.UserData.Add("stage4", stageFourTicks + "");
        return Task.FromResult(new ExtendedResponse<PayloadObject>()
        {
            Response = new PayloadObject(),
            ResponseMetadata = responseMetadata,
        });
    }
};

await rpcExecutor.StartAsync();

Console.WriteLine("Now listening for RPC requests...");

await Task.Delay(TimeSpan.FromMinutes(1));

Console.WriteLine("Shutting down...");

