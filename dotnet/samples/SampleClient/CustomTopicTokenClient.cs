// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Telemetry;
using TestEnvoys.dtmi_com_example_CustomTopicTokens__1;

namespace SampleClient;

internal class CustomTopicTokenClient : CustomTopicTokens.Client
{
    public static Func<IServiceProvider, CounterClient> Factory = service => new CounterClient(service.GetService<MqttSessionClient>()!);

    public CustomTopicTokenClient(MqttSessionClient mqttClient) : base(mqttClient)
    {
        base.CustomTopicTokenMap.TryAdd("ex:myCustomTopicToken", "SomeCustomTopicStringValue");
        TelemetryCollectionReceiver.TopicTokenMap.TryAdd("ex:myCustomTopicToken", "SomeCustomTopicStringValue");
        ReadCustomTopicTokenCommandInvoker.TopicTokenMap.TryAdd("ex:myCustomTopicToken", "SomeCustomTopicStringValue");
        base.CustomTopicTokenMap.TryAdd("myCustomTopicToken", "SomeCustomTopicStringValue");
        TelemetryCollectionReceiver.TopicTokenMap.TryAdd("myCustomTopicToken", "SomeCustomTopicStringValue");
        ReadCustomTopicTokenCommandInvoker.TopicTokenMap.TryAdd("myCustomTopicToken", "SomeCustomTopicStringValue");
    }

    public override Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata)
    {
        // Log or process telemetry data
        Console.WriteLine($"Telemetry received from {senderId}: Cutom topic token value={telemetry.AnnouncedCustomTopicTokenValue}");
        return Task.CompletedTask;
    }
}
