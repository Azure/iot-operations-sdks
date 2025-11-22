using System.Diagnostics;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using SimpleTelemetrySender;

// If you want to log the MQTT layer publishes, subscribes, connects, etc.
bool logMqtt = false;

if (logMqtt) Trace.Listeners.Add(new ConsoleTraceListener());
await using MqttSessionClient mqttClient = new(new MqttSessionClientOptions { EnableMqttLogging = logMqtt });

await mqttClient.ConnectAsync(MqttConnectionSettings.FromEnvVars());

Console.WriteLine("Connected to the MQTT broker");

await using SampleTelemetrySender telemetrySender = new(new(), mqttClient, new Utf8JsonSerializer());

var payloadObject = new PayloadObject()
{
    SomeField = "myTelemetry"
};

Console.WriteLine("Publishing telemetry");
await telemetrySender.SendTelemetryAsync(payloadObject);
Console.WriteLine("Telemetry published successfully");
