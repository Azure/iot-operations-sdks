// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Azure.Iot.Operations.Protocol.RPC;

namespace Azure.Iot.Operations.Protocol.State
{
    public class PropertyReadResponder<TProp, TBool> : CommandExecutor<TBool, TProp>
        where TProp : class
        where TBool : class
    {
        public PropertyReadResponder(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, IPayloadSerializer serializer, string actionTopicToken, Dictionary<string, string> topicTokenMap)
            : base(applicationContext, mqttClient, "read", serializer)
        {
            TopicTokenMap = new(topicTokenMap) { { actionTopicToken, "read" } };
        }
    }
}
