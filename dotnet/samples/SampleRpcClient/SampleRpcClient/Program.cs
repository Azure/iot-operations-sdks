// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Microsoft.Extensions.Configuration;
using SampleRpcClient;

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

await using SampleCommandInvoker rpcInvoker = new(applicationContext, mqttClient, commandName, new Utf8JsonSerializer());

await mqttClient.ConnectAsync(MqttConnectionSettings.FromEnvVars());

var payload = new PayloadObject()
{
    SomeField = "someValue",
    OtherField = "someOtherValue"
};

PayloadObject response = (await rpcInvoker.InvokeCommandAsync(payload)).Response;
Console.WriteLine("Received RPC response");
