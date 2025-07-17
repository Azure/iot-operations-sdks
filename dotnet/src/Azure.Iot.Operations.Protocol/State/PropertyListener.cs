// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Azure.Iot.Operations.Protocol.Telemetry;

namespace Azure.Iot.Operations.Protocol.State
{
    public class PropertyListener<TProp> : TelemetryReceiver<TProp>
        where TProp : class
    {
        public PropertyListener(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, IPayloadSerializer serializer, string actionTopicToken, Dictionary<string, string> topicTokenMap)
            : base(applicationContext, mqttClient, serializer)
        {
            TopicTokenMap = new(topicTokenMap) { { actionTopicToken, "notify" } };
        }
    }
}
