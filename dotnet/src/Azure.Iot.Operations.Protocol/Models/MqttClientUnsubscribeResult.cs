using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Azure.Iot.Operations.Protocol.Models
{
    public class MqttClientUnsubscribeResult(
        ushort packetIdentifier,
        IReadOnlyCollection<MqttClientUnsubscribeResultItem> items,
        string reasonString,
        IReadOnlyCollection<MqttUserProperty> userProperties)
    {

        /// <summary>
        ///     Gets the result for every topic filter item.
        /// </summary>
        public IReadOnlyCollection<MqttClientUnsubscribeResultItem> Items { get; } = items ?? throw new ArgumentNullException(nameof(items));

        /// <summary>
        ///     Gets the packet identifier which was used.
        /// </summary>
        public ushort PacketIdentifier { get; } = packetIdentifier;

        /// <summary>
        ///     Gets the reason string.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public string ReasonString { get; } = reasonString;

        /// <summary>
        ///     Gets the user properties which were part of the UNSUBACK packet.
        ///     <remarks>MQTT 5.0.0+ feature.</remarks>
        /// </summary>
        public IReadOnlyCollection<MqttUserProperty> UserProperties { get; set; } = userProperties ?? throw new ArgumentNullException(nameof(userProperties));

        public void ThrowIfNotSuccessUnsubAck(string? commandName = default)
        {
            if (Items == null || Items.Count == 0)
            {
                throw new AkriMqttException("Received no items in the unsubscribing result, so the unsubscription was unsuccessful.")
                {
                    Kind = AkriMqttErrorKind.MqttError,
                    InApplication = false,
                    IsShallow = false,
                    IsRemote = false,
                    CommandName = commandName,
                };
            }

            foreach (MqttClientUnsubscribeResultItem? unsub in Items)
            {
                if (unsub.ReasonCode != MqttClientUnsubscribeReasonCode.Success)
                {
                    throw new AkriMqttException($"Failed to unsubscribe from topic '{unsub.TopicFilter}' because {unsub.ReasonCode}.")
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

        public bool IsUnsubAckSuccessful()
        {
            if (Items == null || Items.Count == 0)
            {
                Trace.TraceError($"Failed to unsubscribe because no unsubscribing result was received.");
                return false;
            }

            foreach (MqttClientUnsubscribeResultItem? unsub in Items)
            {
                if (unsub.ReasonCode != MqttClientUnsubscribeReasonCode.Success)
                {
                    Trace.TraceError($"Failed to unsubscribe because {unsub.ReasonCode}.");
                    return false;
                }
            }

            return true;
        }
    }
}