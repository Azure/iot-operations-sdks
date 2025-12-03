// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Azure.Iot.Operations.Protocol.Telemetry
{
    /// <summary>
    /// The metadata associated with every message received by a <see cref="TelemetryReceiver{T}"/>.
    /// </summary>
    /// <remarks>
    /// Some metadata should be expected if it was sent by a <see cref="TelemetrySender{T}"/> but may not be
    /// present if the message was sent by something else.
    /// </remarks>
    public class IncomingTelemetryMetadata
    {
        /// <summary>
        /// A timestamp attached to the telemetry message.
        /// </summary>
        /// <remarks>
        /// This value is nullable only because a received message may not have sent it. Any message sent by
        /// <see cref="TelemetrySender{T}"/> will include a non-null timestamp. A message sent by anything else
        /// may or may not include this timestamp.
        /// </remarks>
        public HybridLogicalClock? Timestamp { get; }

        /// <summary>
        /// A dictionary of user properties that are sent along with the telemetry message from the TelemetrySender.
        /// </summary>
        public Dictionary<string, string> UserData { get; }

        /// <summary>
        /// A dictionary of MQTT topic tokens and the replacement values extracted from the publication topic.

        /// </summary>
        public Dictionary<string, string> TopicTokens { get; }

        /// <summary>
        /// The Id of the received MQTT packet.
        /// </summary>
        public uint PacketId { get; }

        /// <summary>
        /// The MQTT client Id of the client that sent this telemetry.
        /// </summary>
        /// <remarks>
        /// This value is null if the received telemetry did not include the <see cref="AkriSystemProperties.SourceId"/> header.
        /// </remarks>
        public string? SenderId { get; internal set; }

        /// <summary>
        /// The content type of the received message if it was sent with a content type.
        /// </summary>
        public string? ContentType { get; internal set; }

        /// <summary>
        /// The payload format indicator of the received message.
        /// </summary>
        public MqttPayloadFormatIndicator PayloadFormatIndicator { get; internal set; }


        internal IncomingTelemetryMetadata(MqttApplicationMessage message, uint packetId, string? topicPattern = null, string? topicNamespace = null)
        {
            UserData = [];

            ContentType = message.ContentType;
            PayloadFormatIndicator = message.PayloadFormatIndicator;

            if (message.UserProperties != null)
            {
                foreach (MqttUserProperty property in message.UserProperties)
                {
                    switch (property.Name)
                    {
                        case AkriSystemProperties.Timestamp:
                            Timestamp = HybridLogicalClock.DecodeFromString(AkriSystemProperties.Timestamp, property.Value);
                            break;
                        case AkriSystemProperties.SourceId:
                            SenderId = property.Value;
                            break;
                        default:
                            if (!AkriSystemProperties.IsReservedUserProperty(property.Name))
                            {
                                UserData[property.Name] = property.Value;
                            }
                            break;
                    }
                }
            }

            if (topicPattern != null)
            {
                string fullTopicPattern = topicPattern;
                if (topicNamespace != null)
                {
                    fullTopicPattern = topicNamespace + "/" + topicPattern;
                }

                TopicTokens = MqttTopicProcessor.GetReplacementMap(fullTopicPattern, message.Topic);
            }
            else
            {
                TopicTokens = new Dictionary<string, string>();
            }

            PacketId = packetId;
        }

        public CloudEvent GetCloudEvent()
        {
            // Convert UserData dictionary to list of MqttUserProperty
            var mqttUserProperties = UserData.Select(kvp => new MqttUserProperty(kvp.Key, kvp.Value)).ToList();
            
            var mqttContext = new CloudEventMqttContext
            {
                MqttUserProperties = mqttUserProperties,
                MqttMessageContentType = ContentType
            };

            try
            {
                return CloudEvent.CreateFromMqttUserProperties(mqttContext);
            }
            catch (ArgumentException ex)
            {
                // Re-throw with telemetry-specific context for backward compatibility
                throw new ArgumentException($"Could not parse cloud event from telemetry: {ex.Message}", ex);
            }
        }
    }
}
