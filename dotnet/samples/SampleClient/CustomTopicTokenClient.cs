// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;
using TestEnvoys.CustomTopicTokens;

namespace SampleClient;

internal class CustomTopicTokenClient : CustomTopicTokens.Client
{
    public CustomTopicTokenClient(ApplicationContext applicationContext, MqttSessionClient mqttClient) : base(applicationContext, mqttClient)
    {
    }

    public override Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata)
    {
        // Log or process telemetry data
        Console.WriteLine($"Telemetry received from {senderId}: Custom topic token value={metadata.TopicTokens["ex:myCustomTopicToken"]}");
        return Task.CompletedTask;
    }
}
