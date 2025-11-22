using System.Diagnostics;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using SimpleTelemetryReceiver;

// If you want to log the MQTT layer publishes, subscribes, connects, etc.
bool logMqtt = false;

if (logMqtt) Trace.Listeners.Add(new ConsoleTraceListener());
await using MqttSessionClient mqttClient = new(new MqttSessionClientOptions { EnableMqttLogging = logMqtt });

await mqttClient.ConnectAsync(MqttConnectionSettings.FromEnvVars());

await using SampleTelemetryReceiver telemetryReceiver = new(new(), mqttClient, new Utf8JsonSerializer());
telemetryReceiver.OnTelemetryReceived += (sourceId, payload, metadata) =>
{
    Console.WriteLine("Received telemetry from source: " + sourceId);
    return Task.CompletedTask;
};

await telemetryReceiver.StartAsync();

Console.WriteLine("Now listening for telemetry...");

await Task.Delay(TimeSpan.FromMinutes(1));

Console.WriteLine("Shutting down...");

