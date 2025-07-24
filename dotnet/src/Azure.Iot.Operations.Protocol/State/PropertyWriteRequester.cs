// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Azure.Iot.Operations.Protocol.RPC;

namespace Azure.Iot.Operations.Protocol.State
{
    public class PropertyWriteRequester<TProp, TBool> : CommandInvoker<TProp, TBool>
        where TProp : class
        where TBool : class
    {
        public PropertyWriteRequester(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, IPayloadSerializer serializer, string actionTopicToken, Dictionary<string, string> topicTokenMap)
            : base(applicationContext, mqttClient, "write", serializer)
        {
            TopicTokenMap = new(topicTokenMap) { { actionTopicToken, "write" } };
        }
    }
}
