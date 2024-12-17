// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Mqtt.Session;
using TestEnvoys.dtmi_com_example_Counter__1;
using Azure.Iot.Operations.Protocol.Telemetry;
using Microsoft.Extensions.Logging;

namespace CounterClient;

public class CounterClient(IMqttPubSubClient mqttClient) : Counter.Client(mqttClient)
{
    private static long telemetryCount = 0;

    public static Func<IServiceProvider, CounterClient> Factory = service => new CounterClient(service.GetService<MqttSessionClient>()!);

    public override Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata)
    {
        // Log or process telemetry data
        Console.WriteLine($"Telemetry received from {senderId}: CounterValue={telemetry.CounterValue}");
        Interlocked.Increment(ref telemetryCount);
        return Task.CompletedTask;
    }

    public long GetTelemetryCount()
    {
        return Interlocked.Read(ref telemetryCount);
    }

}
