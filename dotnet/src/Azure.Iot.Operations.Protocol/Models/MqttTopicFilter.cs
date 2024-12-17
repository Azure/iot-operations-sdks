// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.Models
{
    public class MqttTopicFilter(string topic, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce)
    {

        /// <summary>
        ///     Gets or sets a value indicating whether the sender will not receive its own published application messages.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public bool NoLocal { get; set; }

        /// <summary>
        ///     Gets or sets the quality of service level.
        ///     The Quality of Service (QoS) level is an agreement between the sender of a message and the receiver of a message
        ///     that defines the guarantee of delivery for a specific message.
        ///     There are 3 QoS levels in MQTT:
        ///     - At most once  (0): Message gets delivered no time, once or multiple times.
        ///     - At least once (1): Message gets delivered at least once (one time or more often).
        ///     - Exactly once  (2): Message gets delivered exactly once (It's ensured that the message only comes once).
        /// </summary>
        public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; } = qos;

        /// <summary>
        ///     Gets or sets a value indicating whether messages are retained as published or not.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public bool RetainAsPublished { get; set; }

        /// <summary>
        ///     Gets or sets the retain handling.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public MqttRetainHandling RetainHandling { get; set; }

        /// <summary>
        ///     Gets or sets the MQTT topic.
        ///     In MQTT, the word topic refers to an UTF-8 string that the broker uses to filter messages for each connected
        ///     client.
        ///     The topic consists of one or more topic levels. Each topic level is separated by a forward slash (topic level
        ///     separator).
        /// </summary>
        public string Topic { get; set; } = topic;
    }
}