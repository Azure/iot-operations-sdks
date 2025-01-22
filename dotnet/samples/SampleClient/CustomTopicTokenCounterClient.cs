// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Telemetry;
using TestEnvoys.dtmi_com_example_CustomTopicTokens__1;

namespace SampleClient;

internal class CustomTopicTokenCounterClient : CustomTopicTokens.Client
{
    public static Func<IServiceProvider, CounterClient> Factory = service => new CounterClient(service.GetService<MqttSessionClient>()!);

    public CustomTopicTokenCounterClient(MqttSessionClient mqttClient) : base(mqttClient)
    {
        base.TelemetryCollectionReceiver.TopicTokenMap.Add("ex:myCustomTopicToken", "SomeCustomTopicStringValue");
        base.ReadCustomTopicTokenCommandInvoker.TopicTokenMap.Add("ex:myCustomTopicToken", "SomeCustomTopicStringValue");
    }

    public override Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata)
    {
        // Log or process telemetry data
        Console.WriteLine($"Telemetry received from {senderId}: Cutom topic token value={telemetry.AnnouncedCustomTopicTokenValue}");
        return Task.CompletedTask;
    }
}
