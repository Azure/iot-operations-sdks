// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using SimpleRpcClient;

string commandName = "someCommandName";

// If you want to log the MQTT layer publishes, subscribes, connects, etc.
bool logMqtt = false;

if (logMqtt) Trace.Listeners.Add(new ConsoleTraceListener());
await using MqttSessionClient mqttClient = new(new MqttSessionClientOptions { EnableMqttLogging = logMqtt });

await using SampleCommandInvoker rpcInvoker = new(new(), mqttClient, commandName, new Utf8JsonSerializer());

await mqttClient.ConnectAsync(MqttConnectionSettings.FromEnvVars());

Console.WriteLine("Connected to the MQTT broker");

var payload = new PayloadObject()
{
    SomeField = "someValue",
    OtherField = "someOtherValue"
};

PayloadObject response = (await rpcInvoker.InvokeCommandAsync(payload)).Response;
Console.WriteLine("Received RPC response");
