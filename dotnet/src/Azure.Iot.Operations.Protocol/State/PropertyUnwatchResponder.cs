// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Azure.Iot.Operations.Protocol.RPC;

namespace Azure.Iot.Operations.Protocol.State
{
    public class PropertyUnwatchResponder<TBool> : CommandExecutor<TBool, TBool>
        where TBool : class
    {
        public PropertyUnwatchResponder(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, IPayloadSerializer serializer, string actionTopicToken, Dictionary<string, string> topicTokenMap)
            : base(applicationContext, mqttClient, "unwatch", serializer)
        {
            TopicTokenMap = new(topicTokenMap) { { actionTopicToken, "unwatch" } };
        }
    }
}
