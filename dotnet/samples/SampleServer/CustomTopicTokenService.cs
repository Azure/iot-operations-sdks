﻿// Copyright (c) Microsoft Corporation.c
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Mqtt.Session;
using TestEnvoys.dtmi_com_example_CustomTopicTokens__1;

namespace SampleServer;

public class CustomTopicTokenService : CustomTopicTokens.Service
{
    public CustomTopicTokenService(MqttSessionClient mqttClient) : base(mqttClient) 
    {
        base.CustomTopicTokenMap.TryAdd("ex:myCustomTopicToken", "SomeCustomTopicStringValue");
        base.TelemetryCollectionSender.TopicTokenMap.TryAdd("ex:myCustomTopicToken", "SomeCustomTopicStringValue");
        base.ReadCustomTopicTokenCommandExecutor.TopicTokenMap.TryAdd("ex:myCustomTopicToken", "SomeCustomTopicStringValue");
    }

    public override Task<ExtendedResponse<ReadCustomTopicTokenResponsePayload>> ReadCustomTopicTokenAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string customTopicTokenValue = CustomTopicTokenMap["myCustomTopicToken"];

        return Task.FromResult(new ExtendedResponse<ReadCustomTopicTokenResponsePayload>
        {
            Response = new ReadCustomTopicTokenResponsePayload { CustomTopicTokenResponse = customTopicTokenValue }
        });
    }
}
