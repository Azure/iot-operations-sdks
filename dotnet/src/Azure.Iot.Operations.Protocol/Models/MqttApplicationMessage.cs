// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Telemetry;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Azure.Iot.Operations.Protocol.Models
{
    public class MqttApplicationMessage(string topic, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce)
    {
        private const string AioPersistenceFlag = "aio-persistence";

        /// <summary>
        ///     Gets or sets the content type.
        ///     The content type must be a UTF-8 encoded string. The content type value identifies the kind of UTF-8 encoded
        ///     payload.
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        ///     Gets or sets the correlation data.
        ///     In order for the sender to know what sent message the response refers to it can also send correlation data with the
        ///     published message.
        ///     Hint: MQTT 5 feature only.
        /// </summary>
        public byte[]? CorrelationData { get; set; }

        /// <summary>
        ///     If the DUP flag is set to 0, it indicates that this is the first occasion that the Client or Server has attempted
        ///     to send this MQTT PUBLISH Packet.
        ///     If the DUP flag is set to 1, it indicates that this might be re-delivery of an earlier attempt to send the Packet.
        ///     The DUP flag MUST be set to 1 by the Client or Server when it attempts to re-deliver a PUBLISH Packet
        ///     [MQTT-3.3.1.-1].
        ///     The DUP flag MUST be set to 0 for all QoS 0 messages [MQTT-3.3.1-2].
        /// </summary>
        public bool Dup { get; set; }

        /// <summary>
        ///     Gets or sets the message expiry interval.
        ///     A client can set the message expiry interval in seconds for each PUBLISH message individually.
        ///     This interval defines the period of time that the broker stores the PUBLISH message for any matching subscribers
        ///     that are not currently connected.
        ///     When no message expiry interval is set, the broker must store the message for matching subscribers indefinitely.
        ///     When the retained=true option is set on the PUBLISH message, this interval also defines how long a message is
        ///     retained on a topic.
        ///     Hint: MQTT 5 feature only.
        /// </summary>
        public uint MessageExpiryInterval { get; set; }

        /// <summary>
        ///     Set an ArraySegment as Payload.
        /// </summary>
        public ArraySegment<byte> PayloadSegment
        {
            set { Payload = new ReadOnlySequence<byte>(value); }
        }

        /// <summary>
        ///     Get or set ReadOnlySequence style of Payload.
        ///     This payload type is used internally and is recommended for public use.
        ///     It can be used in combination with a RecyclableMemoryStream to publish
        ///     large buffered messages without allocating large chunks of memory.
        /// </summary>
        public ReadOnlySequence<byte> Payload { get; set; } = ReadOnlySequence<byte>.Empty;

        /// <summary>
        ///     Gets or sets the payload format indicator.
        ///     The payload format indicator is part of any MQTT packet that can contain a payload. The indicator is an optional
        ///     byte value.
        ///     A value of 0 indicates an “unspecified byte stream”.
        ///     A value of 1 indicates a "UTF-8 encoded payload".
        ///     If no payload format indicator is provided, the default value is 0.
        ///     Hint: MQTT 5 feature only.
        /// </summary>
        public MqttPayloadFormatIndicator PayloadFormatIndicator { get; set; } = MqttPayloadFormatIndicator.Unspecified;

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
        ///     Gets or sets the response topic.
        ///     In MQTT 5 the ability to publish a response topic was added in the publish message which allows you to implement
        ///     the request/response pattern between clients that is common in web applications.
        ///     Hint: MQTT 5 feature only.
        /// </summary>
        public string? ResponseTopic { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the message should be retained or not.
        ///     A retained message is a normal MQTT message with the retained flag set to true.
        ///     The broker stores the last retained message and the corresponding QoS for that topic.
        /// </summary>
        public bool Retain { get; set; }

        /// <summary>
        ///     Gets or sets the subscription identifiers.
        ///     The client can specify a subscription identifier when subscribing.
        ///     The broker will establish and store the mapping relationship between this subscription and subscription identifier
        ///     when successfully create or modify subscription.
        ///     The broker will return the subscription identifier associated with this PUBLISH packet and the PUBLISH packet to
        ///     the client when need to forward PUBLISH packets matching this subscription to this client.
        ///     Hint: MQTT 5 feature only.
        /// </summary>
        public List<uint>? SubscriptionIdentifiers { get; set; }

        /// <summary>
        ///     Gets or sets the MQTT topic.
        ///     In MQTT, the word topic refers to an UTF-8 string that the broker uses to filter messages for each connected
        ///     client.
        ///     The topic consists of one or more topic levels. Each topic level is separated by a forward slash (topic level
        ///     separator).
        /// </summary>
        public string Topic { get; set; } = topic;

        /// <summary>
        ///     Gets or sets the topic alias.
        ///     Topic aliases were introduced are a mechanism for reducing the size of published packets by reducing the size of
        ///     the topic field.
        ///     A value of 0 indicates no topic alias is used.
        ///     Hint: MQTT 5 feature only.
        /// </summary>
        public ushort TopicAlias { get; set; }

        /// <summary>
        ///     Gets or sets the user properties.
        ///     In MQTT 5, user properties are basic UTF-8 string key-value pairs that you can append to almost every type of MQTT
        ///     packet.
        ///     As long as you don’t exceed the maximum message size, you can use an unlimited number of user properties to add
        ///     metadata to MQTT messages and pass information between publisher, broker, and subscriber.
        ///     The feature is very similar to the HTTP header concept.
        ///     Hint: MQTT 5 feature only.
        /// </summary>
        public List<MqttUserProperty>? UserProperties { get; set; }

        /// <summary>
        /// If set, this message will be persisted by the AIO MQTT broker. This is only applicable
        /// for retained messages. If this value is set to true, <see cref="Retain"/> must also be set to true.
        /// </summary>
        /// <remarks>
        /// This feature is only applicable with the AIO MQTT broker.
        /// </remarks>
        public bool AioPersistence
        {
            get
            {
                if (UserProperties != null
                    && UserProperties.TryGetProperty(AioPersistenceFlag, out string? value))
                {
                    return value!.Equals("true", StringComparison.Ordinal);
                }

                return false;
            }
            set
            {
                UserProperties ??= new();
                UserProperties.Add(new(AioPersistenceFlag, value ? "true" : "false"));
            }
        }

        /// <summary>
        /// Adds a user property to the message. User properties are key-value pairs that provide
        /// additional metadata for the MQTT message.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        /// <remarks>
        /// Hint: MQTT 5 feature only.
        /// </remarks>
        public void AddUserProperty(string key, string value)
        {
            UserProperties ??= [];
            UserProperties.Add(new MqttUserProperty(key, value));
        }

        /// <summary>
        /// Converts the message payload to a UTF-8 encoded string.
        /// </summary>
        /// <returns>
        /// The payload as a UTF-8 string, or null if the payload is empty.
        /// </returns>
        public string? ConvertPayloadToString()
        {
            return Payload.IsEmpty
                ? null
                : Encoding.UTF8.GetString(Payload.ToArray());
        }

        /// <summary>
        /// Adds telemetry metadata to the message, including timestamp, CloudEvent properties, and custom user data.
        /// </summary>
        /// <param name="md">The outgoing telemetry metadata to add. If null, no action is taken.</param>
        public void AddMetadata(OutgoingTelemetryMetadata? md)
        {
            if (md == null)
            {
                return;
            }
            if (md.Timestamp != default)
            {
                AddUserProperty(AkriSystemProperties.Timestamp, md.Timestamp.EncodeToString());
            }

            if (md.CloudEvent is not null)
            {
                SetCloudEvent(md.CloudEvent);
            }

            foreach (KeyValuePair<string, string> kvp in md.UserData)
            {
                AddUserProperty(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Sets CloudEvent properties on the message by adding them as user properties.
        /// Provides default values for Id and Time if not specified (as per ADR27).
        /// </summary>
        /// <param name="md">The CloudEvent metadata to set on the message.</param>
        /// <remarks>
        /// If the CloudEvent Id is not set, a new GUID will be generated.
        /// If the CloudEvent Time is not set, the current UTC time will be used.
        /// The ContentType property will be overridden if DataContentType is specified in the CloudEvent.
        /// </remarks>
        public void SetCloudEvent(CloudEvent md)
        {
            // Provide default values as per ADR27
            if (string.IsNullOrEmpty(md.Id))
            {
                md.Id = Guid.NewGuid().ToString();
            }

            if (!md.Time.HasValue)
            {
                md.Time = DateTime.UtcNow;
            }

            AddUserProperty(nameof(md.SpecVersion).ToLowerInvariant(), md.SpecVersion);
            if (md.Id != null)
            {
                AddUserProperty(nameof(md.Id).ToLowerInvariant(), md.Id);
            }

            AddUserProperty(nameof(md.Type).ToLowerInvariant(), md.Type);
            AddUserProperty(nameof(md.Source).ToLowerInvariant(), md.Source.ToString());

            if (md.Time is not null)
            {
                AddUserProperty(nameof(md.Time).ToLowerInvariant(), md.Time!.Value.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture));
            }

            if (md.Subject is not null)
            {
                AddUserProperty(nameof(md.Subject).ToLowerInvariant(), md.Subject);
            }

            if (md.DataSchema is not null)
            {
                AddUserProperty(nameof(md.DataSchema).ToLowerInvariant(), md.DataSchema);
            }

            // Override ContentType if DataContentType is set
            if (!string.IsNullOrWhiteSpace(md.DataContentType))
            {
                ContentType = md.DataContentType;
            }
        }

        /// <summary>
        /// Sets ProtocolCloudEvent properties on the message by adding them as user properties.
        /// Used internally by protocol libraries (TelemetrySender, CommandInvoker, CommandExecutor).
        /// Provides default values for Id and Time if not specified.
        /// </summary>
        /// <param name="md">The ProtocolCloudEvent metadata to set on the message.</param>
        /// <remarks>
        /// If the CloudEvent Id is not set, a new GUID will be generated.
        /// If the CloudEvent Time is not set, the current UTC time will be used.
        /// The Type and DataContentType are managed by the protocol library and should not be set by user code.
        /// </remarks>
        internal void SetCloudEvent(ProtocolCloudEvent md)
        {
            // Provide default values as per ADR27
            if (string.IsNullOrEmpty(md.Id))
            {
                md.Id = Guid.NewGuid().ToString();
            }

            if (!md.Time.HasValue)
            {
                md.Time = DateTime.UtcNow;
            }

            AddUserProperty(nameof(md.SpecVersion).ToLowerInvariant(), md.SpecVersion);
            if (md.Id != null)
            {
                AddUserProperty(nameof(md.Id).ToLowerInvariant(), md.Id);
            }

            AddUserProperty(nameof(md.Type).ToLowerInvariant(), md.Type);
            AddUserProperty(nameof(md.Source).ToLowerInvariant(), md.Source.ToString());

            if (md.Time is not null)
            {
                AddUserProperty(nameof(md.Time).ToLowerInvariant(), md.Time!.Value.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture));
            }

            if (md.Subject is not null)
            {
                AddUserProperty(nameof(md.Subject).ToLowerInvariant(), md.Subject);
            }

            if (md.DataSchema is not null)
            {
                AddUserProperty(nameof(md.DataSchema).ToLowerInvariant(), md.DataSchema);
            }
        }

        /// <summary>
        /// Attempts to parse a CloudEvent from the user properties of this MQTT message.
        /// </summary>
        /// <returns>
        /// A CloudEvent object if the required properties (specversion, source, type) are present; otherwise, null.
        /// </returns>
        public CloudEvent? GetCloudEvent()
        {
            if (UserProperties == null || UserProperties.Count == 0)
            {
                return null;
            }

            string? SafeGetUserProperty(string name)
            {
                return UserProperties
                    .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    ?.Value;
            }

            // Get required properties
            string? specVersion = SafeGetUserProperty("specversion");
            if (string.IsNullOrEmpty(specVersion))
            {
                return null;
            }

            if (!specVersion.Equals("1.0", StringComparison.Ordinal))
            {
                // Only version 1.0 is supported
                return null;
            }

            string? sourceValue = SafeGetUserProperty("source");
            if (string.IsNullOrEmpty(sourceValue) || !Uri.TryCreate(sourceValue, UriKind.RelativeOrAbsolute, out Uri? source))
            {
                return null;
            }

            string? type = SafeGetUserProperty("type");
            if (string.IsNullOrEmpty(type))
            {
                return null;
            }

            // Get optional properties
            string? id = SafeGetUserProperty("id");
            string? subject = SafeGetUserProperty("subject");
            string? dataSchema = SafeGetUserProperty("dataschema");
            string? timeValue = SafeGetUserProperty("time");

            DateTime? time = null;
            if (!string.IsNullOrEmpty(timeValue) &&
                DateTime.TryParse(timeValue, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime parsedTime))
            {
                time = parsedTime;
            }

            return new CloudEvent(source, type, specVersion)
            {
                Id = id,
                Time = time,
                DataContentType = ContentType,
                DataSchema = dataSchema,
                Subject = subject
            };
        }

        /// <summary>
        /// Gets the CloudEvent from the MQTT message user properties for protocol use.
        /// This method is used internally by protocol metadata classes and returns a ProtocolCloudEvent
        /// where Type and DataContentType are controlled by the SDK.
        /// </summary>
        /// <returns>A ProtocolCloudEvent parsed from the user properties, or null if CloudEvent properties are not present.</returns>
        internal ProtocolCloudEvent? GetProtocolCloudEvent()
        {
            if (UserProperties == null || UserProperties.Count == 0)
            {
                return null;
            }

            string? SafeGetUserProperty(string name)
            {
                return UserProperties
                    .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    ?.Value;
            }

            // Get required properties
            string? specVersion = SafeGetUserProperty("specversion");
            if (string.IsNullOrEmpty(specVersion))
            {
                return null;
            }

            if (!specVersion.Equals("1.0", StringComparison.Ordinal))
            {
                // Only version 1.0 is supported
                return null;
            }

            string? sourceValue = SafeGetUserProperty("source");
            if (string.IsNullOrEmpty(sourceValue) || !Uri.TryCreate(sourceValue, UriKind.RelativeOrAbsolute, out Uri? source))
            {
                return null;
            }

            string? type = SafeGetUserProperty("type");
            if (string.IsNullOrEmpty(type))
            {
                return null;
            }

            // Get optional properties
            string? id = SafeGetUserProperty("id");
            string? subject = SafeGetUserProperty("subject");
            string? dataSchema = SafeGetUserProperty("dataschema");
            string? timeValue = SafeGetUserProperty("time");

            DateTime? time = null;
            if (!string.IsNullOrEmpty(timeValue) &&
                DateTime.TryParse(timeValue, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime parsedTime))
            {
                time = parsedTime;
            }

            return new ProtocolCloudEvent(source, specVersion)
            {
                Type = type,
                Id = id,
                Time = time,
                DataContentType = ContentType,
                DataSchema = dataSchema,
                Subject = subject
            };
        }
    }
}
