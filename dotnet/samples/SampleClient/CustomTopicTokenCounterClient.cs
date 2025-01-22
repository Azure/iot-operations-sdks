// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Mqtt.Session;
using TestEnvoys.dtmi_com_example_CounterWithCustomTokens__1;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace SampleClient;

internal class CustomTopicTokenCounterClient : CounterWithCustomTokens.Client
{
    public static Func<IServiceProvider, CounterClient> Factory = service => new CounterClient(service.GetService<MqttSessionClient>()!);

    public CustomTopicTokenCounterClient(MqttSessionClient mqttClient) : base(mqttClient)
    {
        CustomTopicTokenMap.Add("myCustomTopicToken", "SomeCustomTopicStringValue");
    }

    public override Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata)
    {
        // Log or process telemetry data
        Console.WriteLine($"Telemetry received from {senderId}: CounterValue={telemetry.CounterValue}");
        return Task.CompletedTask;
    }
}
