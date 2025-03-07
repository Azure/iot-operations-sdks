// Copyright (c) Microsoft Corporation.c
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using TestEnvoys.CustomTopicTokens;

namespace SampleServer;

public class CustomTopicTokenService : CustomTopicTokens.Service
{
    public CustomTopicTokenService(ApplicationContext applicationContext, MqttSessionClient mqttClient) : base(applicationContext, mqttClient)
    {
    }

    public override Task<ExtendedResponse<ReadCustomTopicTokenResponsePayload>> ReadCustomTopicTokenAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string customTopicTokenValue = requestMetadata.TopicTokens["ex:myCustomTopicToken"];
        Console.WriteLine("Received RPC call with custom token value: " + customTopicTokenValue);

        return Task.FromResult(new ExtendedResponse<ReadCustomTopicTokenResponsePayload>
        {
            // Echo the value back to the invoker
            Response = new ReadCustomTopicTokenResponsePayload { CustomTopicTokenResponse = customTopicTokenValue }
        });
    }
}
