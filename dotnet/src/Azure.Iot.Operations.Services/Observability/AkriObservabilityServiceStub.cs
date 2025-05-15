// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.Observability;

public class AkriObservabilityServiceStub : AkriObservabilityService.AkriObservabilityService.Client
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
}
