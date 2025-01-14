// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Mqtt.Session;
using TestEnvoys.dtmi_com_example_Counter__1;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace SampleClient;

internal class CounterClient(IMqttPubSubClient mqttClient) : Counter.Client(mqttClient)
{
    public static Func<IServiceProvider, CounterClient> Factory = service => new CounterClient(service.GetService<MqttSessionClient>()!);

    public override Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata)
    {
        // Log or process telemetry data
        Console.WriteLine($"Telemetry received from {senderId}: CounterValue={telemetry.CounterValue}");
        return Task.CompletedTask;
    }
}
