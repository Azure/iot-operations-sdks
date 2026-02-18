// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Mqtt.Session;
using TestEnvoys.Counter;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace SampleClient;

internal class CounterClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient) : Counter.Client(applicationContext, mqttClient)
{
    public static Func<IServiceProvider, CounterClient> Factory = service => new CounterClient(service.GetRequiredService<ApplicationContext>(), service.GetService<MqttSessionClient>()!);

    public override Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata)
    {
        // Log or process telemetry data
        Console.WriteLine($"Telemetry received from {senderId}: CounterValue={telemetry.CounterValue}");
        return Task.CompletedTask;
    }
}
