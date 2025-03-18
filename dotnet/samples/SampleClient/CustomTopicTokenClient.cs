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
        string customTopicTokenValue = metadata.TopicTokens["ex:myCustomTopicToken"];
        Console.WriteLine("Received telemetry with custom topic token value " + customTopicTokenValue);
        return Task.CompletedTask;
    }
}
