using System;
using System.Diagnostics;

namespace Azure.Iot.Operations.Protocol.Models
{
    public class MqttClientSubscribeResultItem
    {
        public MqttClientSubscribeResultItem(MqttTopicFilter topicFilter, MqttClientSubscribeReasonCode reasonCode)
        {
            TopicFilter = topicFilter ?? throw new ArgumentNullException(nameof(topicFilter));
            ReasonCode = reasonCode;
        }

        /// <summary>
        /// Gets or sets the topic filter.
        /// The topic filter can contain topics and wildcards.
        /// </summary>
        public MqttTopicFilter TopicFilter { get; }

        /// <summary>
        /// Gets or sets the result code.
        /// <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public MqttClientSubscribeReasonCode ReasonCode { get; }
    }
}
