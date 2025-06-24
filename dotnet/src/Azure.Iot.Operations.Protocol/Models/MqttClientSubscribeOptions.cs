// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Azure.Iot.Operations.Protocol.Models
{
    public class MqttClientSubscribeOptions
    {
        private const string AioPersistenceFlag = "aio-persistence";

        public MqttClientSubscribeOptions()
        {
        }

        public MqttClientSubscribeOptions(MqttTopicFilter mqttTopicFilter)
        {
            TopicFilters.Add(mqttTopicFilter);
        }

        public MqttClientSubscribeOptions(string topic, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce)
        {
            TopicFilters.Add(new MqttTopicFilter(topic, qos));
        }

        /// <summary>
        ///     Gets or sets the subscription identifier.
        ///     The client can specify a subscription identifier when subscribing.
        ///     The broker will establish and store the mapping relationship between this subscription and subscription identifier
        ///     when successfully create or modify subscription.
        ///     The broker will return the subscription identifier associated with this PUBLISH packet and the PUBLISH packet to
        ///     the client when need to forward PUBLISH packets matching this subscription to this client.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public uint SubscriptionIdentifier { get; set; }

        /// <summary>
        ///     Gets or sets a list of topic filters the client wants to subscribe to.
        ///     Topic filters can include regular topics or wild cards.
        /// </summary>
        public List<MqttTopicFilter> TopicFilters { get; set; } = [];

        /// <summary>
        ///     Gets or sets the user properties.
        ///     In MQTT 5, user properties are basic UTF-8 string key-value pairs that you can append to almost every type of MQTT
        ///     packet.
        ///     As long as you don’t exceed the maximum message size, you can use an unlimited number of user properties to add
        ///     metadata to MQTT messages and pass information between publisher, broker, and subscriber.
        ///     The feature is very similar to the HTTP header concept.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public List<MqttUserProperty>? UserProperties { get; set; }

        /// <summary>
        /// If set, this subscription will be persisted by the AIO MQTT broker.
        /// </summary>
        /// <remarks>
        /// This feature is only applicable with the AIO MQTT broker.
        /// </remarks>
        public bool AioPersistence
        {
            get
            {
                if (UserProperties == null)
                {
                    return false;
                }

                foreach (MqttUserProperty userProperty in UserProperties)
                {
                    if (userProperty.Name.Equals(AioPersistenceFlag, StringComparison.Ordinal))
                    {
                        if (userProperty.Value.Equals("true", StringComparison.Ordinal))
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }

                return false;
            }
            set
            {
                UserProperties ??= new();
                UserProperties.Add(new(AioPersistenceFlag, value ? "true" : "false"));
            }
        }

        public void AddUserProperty(string key, string value)
        {
            UserProperties ??= [];
            UserProperties.Add(new MqttUserProperty(key, value));
        }
    }
}
