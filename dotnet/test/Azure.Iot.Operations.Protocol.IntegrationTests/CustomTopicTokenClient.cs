﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Telemetry;
using TestEnvoys.CustomTopicTokens;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

internal class CustomTopicTokenClient : CustomTopicTokens.Client
{
    public string CustomTopicTokenValue { get; private set; } = "";

    public TaskCompletionSource OnTelemetryReceived = new();

    public CustomTopicTokenClient(ApplicationContext applicationContext, MqttSessionClient mqttClient) : base(applicationContext, mqttClient)
    {
    }

    public override Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata)
    {
        CustomTopicTokenValue = metadata.TopicTokens["ex:myCustomTopicToken"];
        OnTelemetryReceived.TrySetResult();
        return Task.CompletedTask;
    }
}
