﻿// Copyright (c) Microsoft Corporation.c
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using TestEnvoys.CustomTopicTokens;

namespace Azure.Iot.Operations.Protocol.IntegrationTests;

public class CustomTopicTokenService : CustomTopicTokens.Service
{
    // This is the value of the custom topic token in the most recently received RPC invocation
    public string ReceivedRpcCustomTopicTokenValue { get; private set; } = "";

    // This is the value of the command name topic token in the most recently received RPC invocation
    public string ReceivedRpcCommandNameTopicTokenValue { get; private set; } = "";

    public CustomTopicTokenService(ApplicationContext applicationContext, MqttSessionClient mqttClient) : base(applicationContext, mqttClient)
    {
    }

    public override Task<ExtendedResponse<ReadCustomTopicTokenResponsePayload>> ReadCustomTopicTokenAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        ReceivedRpcCustomTopicTokenValue = requestMetadata.TopicTokens["ex:myCustomTopicToken"];
        ReceivedRpcCommandNameTopicTokenValue = requestMetadata.TopicTokens["commandName"];

        return Task.FromResult(new ExtendedResponse<ReadCustomTopicTokenResponsePayload>
        {
            // Echo the value back to the invoker
            Response = new ReadCustomTopicTokenResponsePayload { CustomTopicTokenResponse = ReceivedRpcCustomTopicTokenValue }
        });
    }
}
