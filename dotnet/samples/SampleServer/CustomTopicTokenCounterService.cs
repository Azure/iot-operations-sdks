// Copyright (c) Microsoft Corporation.c
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Mqtt.Session;
using TestEnvoys.dtmi_com_example_CustomTopicTokens__1;
using TestEnvoys.dtmi_com_example_Counter__1;

namespace SampleServer;

public class CustomTopicTokenCounterService : CustomTopicTokens.Service
{
    public CustomTopicTokenCounterService(MqttSessionClient mqttClient) : base(mqttClient) 
    {
        CustomTopicTokenMap.Add("ex:myCustomTopicToken", "SomeCustomTopicStringValue");
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
