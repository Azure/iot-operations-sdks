// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Telemetry;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
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

        public void AddUserProperty(string key, string value)
        {
            UserProperties ??= [];
            UserProperties.Add(new MqttUserProperty(key, value));
        }

        public string? ConvertPayloadToString()
        {
            return Payload.IsEmpty
                ? null
                : Encoding.UTF8.GetString(Payload.ToArray());
        }

        public void AddMetadata(OutgoingTelemetryMetadata md)
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
                AddCloudEvents(md.CloudEvent);
            }

            foreach (KeyValuePair<string, string> kvp in md.UserData)
            {
                AddUserProperty(kvp.Key, kvp.Value);
            }
        }

        public void AddCloudEvents(CloudEvent md)
        {
            AddUserProperty(nameof(md.SpecVersion).ToLowerInvariant(), md.SpecVersion);
            if (md.Id != null)
            {
                AddUserProperty(nameof(md.Id).ToLowerInvariant(), md.Id.ToString());
            }

            AddUserProperty(nameof(md.Type).ToLowerInvariant(), md.Type);
            AddUserProperty(nameof(md.Source).ToLowerInvariant(), md.Source);

            if (md.Time is not null)
            {
                AddUserProperty(nameof(md.Time).ToLowerInvariant(), md.Time!.Value.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture));
            }

            if (md.Subject is not null)
            {
                AddUserProperty(nameof(md.Subject).ToLowerInvariant(), md.Subject);
            }

            if (md.DataContentType is not null)
            {
                AddUserProperty(nameof(md.DataContentType).ToLowerInvariant(), md.DataContentType);
            }

            if (md.DataSchema is not null)
            {
                AddUserProperty(nameof(md.DataSchema).ToLowerInvariant(), md.DataSchema);
            }
        }
    }
}
