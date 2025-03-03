// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Models
{
    public class MqttClientSubscribeResultItem(MqttTopicFilter topicFilter, MqttClientSubscribeReasonCode reasonCode)
    {

        /// <summary>
        /// Gets or sets the topic filter.
        /// The topic filter can contain topics and wildcards.
        /// </summary>
        public MqttTopicFilter TopicFilter { get; } = topicFilter ?? throw new ArgumentNullException(nameof(topicFilter));

        /// <summary>
        /// Gets or sets the result code.
        /// <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public MqttClientSubscribeReasonCode ReasonCode { get; } = reasonCode;
    }
}
