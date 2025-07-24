// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Azure.Iot.Operations.Protocol.RPC;

namespace Azure.Iot.Operations.Protocol.State
{
    public class PropertyUnobserveRequester<TBool> : CommandInvoker<TBool, TBool>
        where TBool : class
    {
        public PropertyUnobserveRequester(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, IPayloadSerializer serializer, string actionTopicToken, Dictionary<string, string> topicTokenMap)
            : base(applicationContext, mqttClient, "unobserve", serializer)
        {
            TopicTokenMap = new(topicTokenMap) { { actionTopicToken, "unobserve" } };
        }
    }
}
