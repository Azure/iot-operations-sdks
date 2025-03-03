// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
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


        internal IncomingTelemetryMetadata(MqttApplicationMessage message, uint packetId, string? topicPattern = null)
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

            TopicTokens = topicPattern != null ? MqttTopicProcessor.GetReplacementMap(topicPattern, message.Topic) : new Dictionary<string, string>();

            PacketId = packetId;
        }

        public CloudEvent GetCloudEvent()
        {
            string safeGetUserProperty(string name)
            {
                return UserData.FirstOrDefault(
                                p => p.Key.Equals(name.ToLowerInvariant(),
                                StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
            }

            string specVersion = safeGetUserProperty(nameof(CloudEvent.SpecVersion).ToLowerInvariant());


            if (specVersion != "1.0")
            {
                throw new ArgumentException($"Could not parse cloud event from telemetry: Only version 1.0 supported. Version provided: {specVersion}");
            }

            string id = safeGetUserProperty(nameof(CloudEvent.Id));
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("Could not parse cloud event from telemetry: Cloud events must have an Id");
            }

            string sourceValue = safeGetUserProperty(nameof(CloudEvent.Source));
            if (!Uri.TryCreate(sourceValue, UriKind.RelativeOrAbsolute, out Uri? source))
            {
                throw new ArgumentException("Could not parse cloud event from telemetry: Source must be a URI-Reference");
            }

            string type = safeGetUserProperty(nameof(CloudEvent.Type));
            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentException("Could not parse cloud event from telemetry: Cloud events must specify a Type");
            }

            string subject = safeGetUserProperty(nameof(CloudEvent.Subject));
            string dataSchema = safeGetUserProperty(nameof(CloudEvent.DataSchema));

            string time = safeGetUserProperty(nameof(CloudEvent.Time));
            DateTime _dateTime = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(time) && !DateTime.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out _dateTime))
            {
                throw new ArgumentException("Could not parse cloud event from telemetry: Cloud events time must be a valid RFC3339 date-time");
            }

            return new CloudEvent(source, type)
            {
                Id = id,
                Time = _dateTime,
                DataContentType = ContentType,
                DataSchema = dataSchema,
                Subject = subject,
            };
        }
    }
}
