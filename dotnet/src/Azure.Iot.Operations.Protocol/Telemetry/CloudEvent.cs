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
    /// Implements the CloudEvent spec 1.0. The required fields are source, type, id and specversion.
    /// Id is required but we want to update it in the same instance. 
    /// See <a href="https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md">CloudEvent Spec</a>
    /// </summary>
    /// <param name="source"><see cref="Source"/></param>
    /// <param name="type"><see cref="Type"/></param>
    /// <param name="specversion"><see cref="SpecVersion"/></param>
    public class CloudEvent(Uri source, string type = "ms.aio.telemetry", string specversion = "1.0")
    {
        private const string CloudEventIdProperty = "id";
        private const string CloudEventSourceProperty = "source";
        private const string CloudEventSpecVersionProperty = "specversion";
        private const string CloudEventTypeProperty = "type";
        private const string CloudEventTimeProperty = "time";
        private const string CloudEventDataContentTypeProperty = "datacontenttype";
        private const string CloudEventSubjectProperty = "subject";
        private const string CloudEventDataSchemaProperty = "dataschema";

        /// <summary>
        /// Identifies the context in which an event happened.
        /// Often this will include information such as the type of the event source, 
        /// the organization publishing the event or the process that produced the event. 
        /// The exact syntax and semantics behind the data encoded in the URI is defined by the event producer.
        /// </summary>
        public Uri Source => source;

        /// <summary>The version of the CloudEvents specification which the event uses. 
        /// This enables the interpretation of the context. 
        /// Compliant event producers MUST use a value of 1.0 when referring to this version of the specification.
        /// </summary>
        public string SpecVersion => specversion;

        /// <summary>
        /// Contains a value describing the type of event related to the originating occurrence. 
        /// Often this attribute is used for routing, observability, policy enforcement, etc. 
        /// The format of this is producer defined and might include information such as the version of the type
        /// </summary>
        public string Type => type;

        /// <summary>
        ///  Identifies the event. Producers MUST ensure that source + id is unique for each distinct event. 
        ///  If a duplicate event is re-sent (e.g. due to a network error) it MAY have the same id. 
        ///  Consumers MAY assume that Events with identical source and id are duplicates.
        /// </summary>
        public string? Id { get; set; } = null!; // although id is required, we want update it in the same instance from the sender.

        /// <summary>
        /// Timestamp of when the occurrence happened. 
        /// If the time of the occurrence cannot be determined then this attribute MAY be set to some other time (such as the current time) 
        /// by the CloudEvents producer, 
        /// however all producers for the same source MUST be consistent in this respect. 
        /// </summary>
        public DateTime? Time { get; set; }

        /// <summary>
        ///  Content type of data value. This attribute enables data to carry any type of content, 
        ///  whereby format and encoding might differ from that of the chosen event format.
        /// </summary>
        public string? DataContentType { get; internal set; }

        /// <summary>
        /// Identifies the subject of the event in the context of the event producer (identified by source). 
        /// In publish-subscribe scenarios, a subscriber will typically subscribe to events emitted by a source, 
        /// but the source identifier alone might not be sufficient as a qualifier for any specific event if the source context has internal sub-structure.
        /// </summary>
        public string? Subject { get; set; }

        /// <summary>
        ///  Identifies the schema that data adheres to. 
        ///  Incompatible changes to the schema SHOULD be reflected by a different URI.
        /// </summary>
        public string? DataSchema { get; set; }

        /// <summary>
        /// Creates an MQTT message context containing user properties and content type from this CloudEvent.
        /// This method provides default values for id (new GUID), time (current UTC time), and specversion (1.0) if not already set.
        /// </summary>
        /// <returns>A <see cref="CloudEventMqttContext"/> containing the MQTT user properties and content type.</returns>
        /// <exception cref="ArgumentException">Thrown if required fields are missing or invalid.</exception>
        public CloudEventMqttContext CreateMqttMessageContext()
        {
            // Validate required fields
            if (Source == null)
            {
                throw new ArgumentException("CloudEvent Source is required and cannot be null.");
            }

            if (string.IsNullOrEmpty(Type))
            {
                throw new ArgumentException("CloudEvent Type is required and cannot be null or empty.");
            }

            if (string.IsNullOrEmpty(SpecVersion))
            {
                throw new ArgumentException("CloudEvent SpecVersion is required and cannot be null or empty.");
            }

            // Create user properties list
            List<MqttUserProperty> userProperties = new();

            // Add required fields
            userProperties.Add(new MqttUserProperty(CloudEventSpecVersionProperty, SpecVersion));
            userProperties.Add(new MqttUserProperty(CloudEventTypeProperty, Type));
            userProperties.Add(new MqttUserProperty(CloudEventSourceProperty, Source.ToString()));

            // Add id with default if not set
            string idValue = Id ?? Guid.NewGuid().ToString();
            userProperties.Add(new MqttUserProperty(CloudEventIdProperty, idValue));

            // Add time with default if not set
            DateTime timeValue = Time ?? DateTime.UtcNow;
            userProperties.Add(new MqttUserProperty(CloudEventTimeProperty, timeValue.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture)));

            // Add optional fields if present
            if (!string.IsNullOrEmpty(Subject))
            {
                userProperties.Add(new MqttUserProperty(CloudEventSubjectProperty, Subject));
            }

            if (!string.IsNullOrEmpty(DataSchema))
            {
                userProperties.Add(new MqttUserProperty(CloudEventDataSchemaProperty, DataSchema));
            }

            // Note: DataContentType is not added to user properties per CloudEvents MQTT spec
            // It should be set as the MQTT message's ContentType property

            return new CloudEventMqttContext
            {
                MqttUserProperties = userProperties,
                MqttMessageContentType = DataContentType
            };
        }

        /// <summary>
        /// Creates a CloudEvent from MQTT message context (user properties and content type).
        /// </summary>
        /// <param name="mqttMessageContext">The MQTT message context containing user properties and content type.</param>
        /// <returns>A <see cref="CloudEvent"/> parsed from the MQTT message context.</returns>
        /// <exception cref="ArgumentNullException">Thrown if mqttMessageContext is null.</exception>
        /// <exception cref="ArgumentException">Thrown if required CloudEvent fields are missing or invalid.</exception>
        public static CloudEvent CreateFromMqttUserProperties(CloudEventMqttContext mqttMessageContext)
        {
            ArgumentNullException.ThrowIfNull(mqttMessageContext);

            if (mqttMessageContext.MqttUserProperties == null)
            {
                throw new ArgumentException("MqttUserProperties cannot be null.", nameof(mqttMessageContext));
            }

            // Helper function to safely get user property
            string? GetUserProperty(string propertyName)
            {
                return mqttMessageContext.MqttUserProperties
                    .FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))?.Value;
            }

            // Validate and get required fields
            string? specVersion = GetUserProperty(CloudEventSpecVersionProperty);
            if (string.IsNullOrEmpty(specVersion))
            {
                throw new ArgumentException($"CloudEvent '{CloudEventSpecVersionProperty}' is required.");
            }

            if (!specVersion.Equals("1.0", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Only CloudEvent spec version 1.0 is supported. Version provided: {specVersion}");
            }

            string? id = GetUserProperty(CloudEventIdProperty);
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException($"CloudEvent '{CloudEventIdProperty}' is required.");
            }

            string? sourceValue = GetUserProperty(CloudEventSourceProperty);
            if (string.IsNullOrEmpty(sourceValue))
            {
                throw new ArgumentException($"CloudEvent '{CloudEventSourceProperty}' is required.");
            }

            if (!Uri.TryCreate(sourceValue, UriKind.RelativeOrAbsolute, out Uri? sourceUri))
            {
                throw new ArgumentException($"CloudEvent '{CloudEventSourceProperty}' must be a valid URI. Value provided: {sourceValue}");
            }

            string? typeValue = GetUserProperty(CloudEventTypeProperty);
            if (string.IsNullOrEmpty(typeValue))
            {
                throw new ArgumentException($"CloudEvent '{CloudEventTypeProperty}' is required.");
            }

            // Create the CloudEvent with required fields
            CloudEvent cloudEvent = new(sourceUri, typeValue, specVersion)
            {
                Id = id
            };

            // Parse optional time field
            string? timeValue = GetUserProperty(CloudEventTimeProperty);
            if (!string.IsNullOrEmpty(timeValue))
            {
                if (!DateTime.TryParse(timeValue, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime parsedTime))
                {
                    throw new ArgumentException($"CloudEvent '{CloudEventTimeProperty}' must be a valid RFC3339 date-time. Value provided: {timeValue}");
                }
                cloudEvent.Time = parsedTime;
            }

            // Parse optional fields
            cloudEvent.Subject = GetUserProperty(CloudEventSubjectProperty);
            cloudEvent.DataSchema = GetUserProperty(CloudEventDataSchemaProperty);
            cloudEvent.DataContentType = mqttMessageContext.MqttMessageContentType;

            return cloudEvent;
        }
    }

    /// <summary>
    /// Represents the MQTT message context for a CloudEvent, including user properties and content type.
    /// </summary>
    public class CloudEventMqttContext
    {
        /// <summary>
        /// Gets or sets the MQTT user properties that represent CloudEvent fields.
        /// </summary>
        public List<MqttUserProperty> MqttUserProperties { get; set; } = new();

        /// <summary>
        /// Gets or sets the MQTT message content type, which corresponds to the CloudEvent datacontenttype field.
        /// </summary>
        public string? MqttMessageContentType { get; set; }
    }
}
