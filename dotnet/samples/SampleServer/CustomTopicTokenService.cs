// Copyright (c) Microsoft Corporation.c
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Mqtt.Session;
using TestEnvoys.dtmi_com_example_CustomTopicTokens__1;
using Azure.Iot.Operations.Protocol;

namespace SampleServer;

public class CustomTopicTokenService : CustomTopicTokens.Service
{
    public CustomTopicTokenService(ApplicationContext applicationContext, MqttSessionClient mqttClient) : base(applicationContext, mqttClient) 
    {
    }

    public override Task<ExtendedResponse<ReadCustomTopicTokenResponsePayload>> ReadCustomTopicTokenAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string customTopicTokenValue1 = requestMetadata.TopicTokens["ex:myCustomTopicToken"];
        string customTopicTokenValue2 = requestMetadata.TopicTokens["ex:myCustomTopicToken"];
        Console.WriteLine("Received RPC call with token values " + customTopicTokenValue1 + " " + customTopicTokenValue2);
        return Task.FromResult(new ExtendedResponse<ReadCustomTopicTokenResponsePayload>
        {
            Response = new ReadCustomTopicTokenResponsePayload { CustomTopicTokenResponse = customTopicTokenValue1 }
        });
    }
}
