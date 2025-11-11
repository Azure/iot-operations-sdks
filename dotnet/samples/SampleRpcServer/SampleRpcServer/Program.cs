using System.Diagnostics;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Microsoft.Extensions.Configuration;
using SampleRpcServer;

string commandName = "someCommandName";

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var mqttDiag = Convert.ToBoolean(configuration["mqttDiag"]);
if (mqttDiag) Trace.Listeners.Add(new ConsoleTraceListener());
await using MqttSessionClient mqttClient = new(new MqttSessionClientOptions { EnableMqttLogging = mqttDiag });
ApplicationContext applicationContext = new();

await mqttClient.ConnectAsync(MqttConnectionSettings.FromEnvVars());

await using SampleCommandExecutor rpcExecutor = new(applicationContext, mqttClient, commandName, new Utf8JsonSerializer())
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

await Task.Delay(TimeSpan.FromMinutes(1));




