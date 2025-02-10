// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Telemetry;
using TestEnvoys.Counter;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class CounterClient(IMqttPubSubClient mqttClient) : Counter.Client(mqttClient)
{
    public override Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata)
    {
        // Log or process telemetry data
        Console.WriteLine($"Telemetry received from {senderId}: CounterValue={telemetry.CounterValue}");
        return Task.CompletedTask;
    }
}
