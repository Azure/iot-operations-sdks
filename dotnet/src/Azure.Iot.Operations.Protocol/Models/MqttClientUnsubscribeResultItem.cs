using System;

namespace Azure.Iot.Operations.Protocol.Models
{
    public class MqttClientUnsubscribeResultItem
    {
        public MqttClientUnsubscribeResultItem(string topicFilter, MqttClientUnsubscribeReasonCode reasonCode)
        {
            TopicFilter = topicFilter ?? throw new ArgumentNullException(nameof(topicFilter));
            ReasonCode = reasonCode;
        }

        /// <summary>
        ///     Gets or sets the result code.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public MqttClientUnsubscribeReasonCode ReasonCode { get; }

        /// <summary>
        ///     Gets or sets the topic filter.
        ///     The topic filter can contain topics and wildcards.
        /// </summary>
        public string TopicFilter { get; }
    }
}