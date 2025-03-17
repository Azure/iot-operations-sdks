// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;
using TestEnvoys.CustomTopicTokens;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

internal class CustomTopicTokenClient : CustomTopicTokens.Client
{
    // This is the value of the custom topic token in the most recently received telemetry
    public string CustomTopicTokenValue { get; private set; } = "";

    // This TCS triggers upon the first telemetry received
    public TaskCompletionSource OnTelemetryReceived = new();

    public CustomTopicTokenClient(ApplicationContext applicationContext, MqttSessionClient mqttClient, Dictionary<string, string>? topicTokenMap = null) : base(applicationContext, mqttClient, topicTokenMap)
    {
    }

    public override Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata)
    {
        CustomTopicTokenValue = metadata.TopicTokens["ex:myCustomTopicToken"];
        OnTelemetryReceived.TrySetResult();
        return Task.CompletedTask;
    }
}
