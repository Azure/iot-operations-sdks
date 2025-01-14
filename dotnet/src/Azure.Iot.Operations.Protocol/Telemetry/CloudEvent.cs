// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Azure.Iot.Operations.Protocol.Telemetry
{
    /// <summary>
    /// Implements the CloudEvent spec 1.0.
    /// See <a href="https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md">CloudEvent Spec</a>
    /// </summary>
    public class CloudEvent
    {
        /// <summary>
        /// Identifies the context in which an event happened.
        /// Often this will include information such as the type of the event source, 
        /// the organization publishing the event or the process that produced the event. 
        /// The exact syntax and semantics behind the data encoded in the URI is defined by the event producer.
        /// </summary>
        public Uri Source { get; set; }

        /// <summary>The version of the CloudEvents specification which the event uses. 
        /// This enables the interpretation of the context. 
        /// Compliant event producers MUST use a value of 1.0 when referring to this version of the specification.
        /// </summary>
        public string SpecVersion { get; set; }

        /// <summary>
        /// Contains a value describing the type of event related to the originating occurrence. 
        /// Often this attribute is used for routing, observability, policy enforcement, etc. 
        /// The format of this is producer defined and might include information such as the version of the type
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        ///  Identifies the event. Producers MUST ensure that source + id is unique for each distinct event. 
        ///  If a duplicate event is re-sent (e.g. due to a network error) it MAY have the same id. 
        ///  Consumers MAY assume that Events with identical source and id are duplicates.
        /// </summary>
        /// <remarks>
        /// By default, a random GUID will be used as the value.
        /// </remarks>
        public string Id { get; internal set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Timestamp of when the occurrence happened. 
        /// If the time of the occurrence cannot be determined then this attribute MAY be set to some other time (such as the current time) 
        /// by the CloudEvents producer, 
        /// however all producers for the same source MUST be consistent in this respect. 
        /// </summary>
        public DateTime? Time { get; internal set; } = DateTime.UtcNow;

        /// <summary>
        ///  Content type of data value. This attribute enables data to carry any type of content, 
        ///  whereby format and encoding might differ from that of the chosen event format.
        /// </summary>
        /// <remarks>
        /// This value will be the content type associated with the MQTT message that this cloud event was associated with.
        /// </remarks>
        public string? DataContentType { get; internal set; } //  Default value should be the serializer's content type, but this class isn't tied to a serializer yet.

        /// <summary>
        /// Identifies the subject of the event in the context of the event producer (identified by source). 
        /// In publish-subscribe scenarios, a subscriber will typically subscribe to events emitted by a source, 
        /// but the source identifier alone might not be sufficient as a qualifier for any specific event if the source context has internal sub-structure.
        /// </summary>
        public string? Subject { get; internal set; } //  Default value should be the telemetry sender/receiver's topic, but this class isn't tied to a telemetry sender/receiver yet.

        /// <summary>
        ///  Identifies the schema that data adheres to. 
        ///  Incompatible changes to the schema SHOULD be reflected by a different URI.
        /// </summary>
        public string? DataSchema { get; set; }

        private const string CloudEventsDataContentTypeRegex = "^([-a-z]+)/([-a-z0-9\\.\\-]+)(?:\\+([a-z0-9\\.\\-]+))?$";

        public CloudEvent(Uri source, string type = "ms.aio.telemetry", string specversion = "1.0")
        {
            Source = source;
            Type = type;
            SpecVersion = specversion;
        }

        /// <summary>
        /// Construct a cloud event using the metadata received on an MQTT message.
        /// </summary>
        /// <param name="contentType">The content type of the MQTT message.</param>
        /// <param name="userProperties">The user properties of the MQTT message.</param>
        /// <exception cref="ArgumentException">Thrown if the provided content type or user properties cannot be used to construct a valid cloud event.</exception>
        public CloudEvent(string? contentType, Dictionary<string, string> userProperties)
        {
            string safeGetUserProperty(string name)
            {
                return userProperties.FirstOrDefault(
                                p => p.Key.Equals(name.ToLowerInvariant(),
                                StringComparison.OrdinalIgnoreCase)).Value ?? string.Empty;
            }

            SpecVersion = safeGetUserProperty(nameof(CloudEvent.SpecVersion).ToLowerInvariant());

            if (SpecVersion == "1.0")
            {
                string id = safeGetUserProperty(nameof(CloudEvent.Id));
                if (string.IsNullOrEmpty(id))
                {
                    throw new ArgumentException($"Invalid cloud event: missing Id field");
                }

                string sourceValue = safeGetUserProperty(nameof(CloudEvent.Source));
                if (!Uri.TryCreate(sourceValue, UriKind.RelativeOrAbsolute, out Uri? source))
                {
                    throw new ArgumentException($"Invalid cloud event: source field must be a URI-reference");
                }
                string type = safeGetUserProperty(nameof(CloudEvent.Type));
                if (string.IsNullOrEmpty(type))
                {
                    throw new ArgumentException($"Invalid cloud event: no type specified");
                }

                string subject = safeGetUserProperty(nameof(CloudEvent.Subject));
                string dataSchema = safeGetUserProperty(nameof(CloudEvent.DataSchema));

                string time = safeGetUserProperty(nameof(CloudEvent.Time));
                DateTime _dateTime = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(time) && !DateTime.TryParse(time, CultureInfo.InvariantCulture, out _dateTime))
                {
                    throw new ArgumentException($"Invalid cloud event: time must be a valid RFC3339 date-time");
                }

                Source = source;
                Type = type;
                Id = id;
                Time = _dateTime;
                DataContentType = contentType;
                DataSchema = dataSchema;
                Subject = subject;
            }
            else
            {
                throw new ArgumentException($"Invalid cloud event: unsupported cloud event spec version: {SpecVersion}");
            }
        }

        /// <summary>
        /// Converts the CloudEvent to a dictionary of user properties that can be used to create an MQTT message.
        /// </summary>
        /// <returns>The dictionary of user properties to add to your outgoing telemetry message.</returns>
        public Dictionary<string, string> ToUserProperties()
        {
            var userProperties = new Dictionary<string, string>
            {
                { nameof(SpecVersion).ToLowerInvariant(), SpecVersion },
                { nameof(Id).ToLowerInvariant(), Id!.ToString() },
                {  nameof(Type).ToLowerInvariant(), Type },
                { nameof(Source).ToLowerInvariant(), Source.ToString() },
            };

            if (Time is not null)
            {
                userProperties[nameof(Time).ToLowerInvariant()] = Time!.Value.ToString("O");
            }

            if (Subject is not null)
            {
                userProperties[nameof(Subject).ToLowerInvariant()] = Subject;
            }

            if (DataSchema is not null)
            {
                userProperties[nameof(DataSchema).ToLowerInvariant()] = DataSchema;
            }

            return userProperties;
        }
    }
}
