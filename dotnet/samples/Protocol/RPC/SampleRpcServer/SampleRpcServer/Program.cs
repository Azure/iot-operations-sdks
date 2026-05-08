using System.Diagnostics;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using SimpleRpcServer;

string commandName = "someCommandName";

// If you want to log the MQTT layer publishes, subscribes, connects, etc.
bool logMqtt = false;

if (logMqtt) Trace.Listeners.Add(new ConsoleTraceListener());
await using MqttSessionClient mqttClient = new(new MqttSessionClientOptions { EnableMqttLogging = logMqtt });

await mqttClient.ConnectAsync(MqttConnectionSettings.FromEnvVars());

Console.WriteLine("Connected to the MQTT broker");

await using SampleCommandExecutor rpcExecutor = new(new(), mqttClient, commandName, new Utf8JsonSerializer())
{
    OnCommandReceived = (request, cancellationToken) =>
    {
        Console.WriteLine("Received RPC request");

        var responsePayload = new PayloadObject()
        {
            SomeField = request.Request.SomeField,
            OtherField = request.Request.OtherField,
        };

        // Echo the payload back to the sender
        return Task.FromResult(new Azure.Iot.Operations.Protocol.RPC.ExtendedResponse<PayloadObject>()
        {
            Response = responsePayload
        });
    }
};

await rpcExecutor.StartAsync();

Console.WriteLine("Now listening for RPC requests...");

await Task.Delay(TimeSpan.FromMinutes(1));

Console.WriteLine("Shutting down...");

