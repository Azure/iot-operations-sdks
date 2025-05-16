// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Services.Observability.AkriObservabilityService;

namespace Azure.Iot.Operations.Services.Observability;

public class AkriObservabilityServiceStub : AkriObservabilityService.AkriObservabilityService.Client, IAkriObservabilityService
{
    public AkriObservabilityServiceStub(
        ApplicationContext applicationContext,
        IMqttPubSubClient mqttClient,
        Dictionary<string, string>? topicTokenMap = null) : base(
            applicationContext,
            mqttClient,
            topicTokenMap)
    {
    }

    public RpcCallAsync<PublishMetricsResponsePayload> PublishMetricsAsync(PublishMetricsRequestPayload request)
    {
        return base.PublishMetricsAsync(request);
    }
}
