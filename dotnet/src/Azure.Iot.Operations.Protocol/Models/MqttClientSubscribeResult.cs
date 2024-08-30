using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Azure.Iot.Operations.Protocol.Models
{
    public class MqttClientSubscribeResult
    {
        public MqttClientSubscribeResult(ushort packetIdentifier, IReadOnlyCollection<MqttClientSubscribeResultItem> items, string reasonString, IReadOnlyCollection<MqttUserProperty> userProperties)
        {
            PacketIdentifier = packetIdentifier;
            Items = items ?? throw new ArgumentNullException(nameof(items));
            ReasonString = reasonString;
            UserProperties = userProperties ?? throw new ArgumentNullException(nameof(userProperties));
        }

        /// <summary>
        /// Gets the result for every topic filter item.
        /// </summary>
        public IReadOnlyCollection<MqttClientSubscribeResultItem> Items { get; }
        
        /// <summary>
        /// Gets the user properties which were part of the SUBACK packet.
        /// <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public IReadOnlyCollection<MqttUserProperty> UserProperties { get; }
        
        /// <summary>
        /// Gets the reason string.
        /// <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public string ReasonString { get; }

        /// <summary>
        /// Gets the packet identifier which was used.
        /// </summary>
        public ushort PacketIdentifier { get; }

        public void ThrowIfNotSuccessSubAck(MqttQualityOfServiceLevel requestedQos, string? commandName = default)
        {
            if (Items == null || Items.Count == 0)
            {
                throw new AkriMqttException("Received no items in the subscribing result, so the subscription was unsuccessful.")
                {
                    Kind = AkriMqttErrorKind.MqttError,
                    InApplication = false,
                    IsShallow = false,
                    IsRemote = false,
                    CommandName = commandName,
                };
            }

            foreach (MqttClientSubscribeResultItem? sub in Items)
            {
                if (!IsSubscriptionSuccessful(sub, requestedQos))
                {
                    throw new AkriMqttException($"Failed to subscribe to topic '{sub.TopicFilter.Topic}' because {sub.ReasonCode}.")
                    {
                        Kind = AkriMqttErrorKind.MqttError,
                        InApplication = false,
                        IsShallow = false,
                        IsRemote = false,
                        CommandName = commandName,
                    };
                }
            }
        }

        public bool IsSubAckSuccessful(MqttQualityOfServiceLevel requestedQos)
        {
            if (Items == null || Items.Count == 0)
            {
                return false;
            }

            foreach (MqttClientSubscribeResultItem? sub in Items)
            {
                if (!IsSubscriptionSuccessful(sub, requestedQos))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSubscriptionSuccessful(MqttClientSubscribeResultItem subscribeResultItem, MqttQualityOfServiceLevel requestedQos)
        {
            var resultCode = subscribeResultItem.ReasonCode;

            // The granted QoS level is different from the requested QoS level
            if (((Int32)resultCode).CompareTo((Int32)requestedQos) != 0)
            {
                Trace.TraceWarning($"The granted QoS level [{resultCode}] is different from the requested QoS level [{requestedQos}].");
            }

            return resultCode == MqttClientSubscribeReasonCode.GrantedQoS0
                || resultCode == MqttClientSubscribeReasonCode.GrantedQoS1
                || resultCode == MqttClientSubscribeReasonCode.GrantedQoS2;
        }
    }
}