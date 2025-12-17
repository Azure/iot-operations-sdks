// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol
{
    /// <summary>
    /// Implements the CloudEvent spec 1.0. The required fields are source, type, id and specversion.
    /// Id is required but we want to update it in the same instance.
    /// See <a href="https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md">CloudEvent Spec</a>
    /// </summary>
    /// <param name="source"><see cref="Source"/></param>
    /// <param name="type"><see cref="Type"/></param>
    /// <param name="specversion"><see cref="SpecVersion"/></param>
    public class CloudEvent(Uri source, string type = "", string specversion = "1.0")
    {
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
        public string SpecVersion
        {
            get
            {
                return _specVersion;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("SpecVersion must not be an empty string");
                }

                _specVersion = value;
            }
        }

        private string _specVersion = specversion;

        /// <summary>
        /// Contains a value describing the type of event related to the originating occurrence.
        /// Often this attribute is used for routing, observability, policy enforcement, etc.
        /// The format of this is producer defined and might include information such as the version of the type
        /// </summary>
        /// <remarks>
        /// This value cannot be null or whitespace
        /// </remarks>
        public string Type
        {
            get
            {
                return _type;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("The type cannot be null or whitespace");
                }

                _type = value;
            }
        }

        private string _type = type;

        /// <summary>
        ///  Identifies the event. Producers MUST ensure that source + id is unique for each distinct event.
        ///  If a duplicate event is re-sent (e.g. due to a network error) it MAY have the same id.
        ///  Consumers MAY assume that Events with identical source and id are duplicates.
        /// </summary>
        public string? Id
        {
            get
            {
                return _id;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Id cannot be null or whitespace");
                }

                _id = value;
            }
        }

        private string _id = Guid.NewGuid().ToString();

        /// <summary>
        /// Timestamp of when the occurrence happened.
        /// If the time of the occurrence cannot be determined then this attribute MAY be set to some other time (such as the current time)
        /// by the CloudEvents producer,
        /// however all producers for the same source MUST be consistent in this respect.
        /// </summary>
        /// <remarks>
        /// By default, this value is set to the current UTC time. This field can be set to null if you don't want the cloud event to include a time.
        /// </remarks>
        public DateTime? Time { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Identifies the subject of the event in the context of the event producer (identified by source).
        /// In publish-subscribe scenarios, a subscriber will typically subscribe to events emitted by a source,
        /// but the source identifier alone might not be sufficient as a qualifier for any specific event if the source context has internal sub-structure.
        /// </summary>
        /// <remarks>
        /// This value can be set to null if no subject should be sent.
        ///
        /// When this cloud event is sent by a telemetry sender/command invoker/command executor, a default
        /// value of the MQTT topic this event is sent on will be filled in for you if no other value is explicitly
        /// set.
        /// </remarks>
        public string? Subject
        {
            get
            {
                return _subject;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value) && value != null) // if value is whitespace
                {
                    throw new ArgumentException("Subject cannot be whitespace");
                }

                IsSubjectDefault = false;
                _subject = value;
            }
        }

        private string? _subject;

        // Used to track if the user has provided any explicit value for the Subject field. If they haven't,
        // then it will be filled in for them in the telemetry sender/command invoker/command executor.
        internal bool IsSubjectDefault = true;

        /// <summary>
        ///  Identifies the schema that data adheres to.
        ///  Incompatible changes to the schema SHOULD be reflected by a different URI.
        /// </summary>
        /// <remarks>
        /// This value must resolve to a valid URI.
        /// </remarks>
        public string? DataSchema
        {
            get
            {
                return _dataSchema;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _dataSchema = null;
                    return;
                }

                try
                {
                    // non-null non-empty values should resolve to a URI
                    _ = new Uri(value);
                    _dataSchema = value;
                }
                catch (UriFormatException)
                {
                    throw new ArgumentException("The provided data schema does not resolve to a URI");
                }
            }
        }

        private string? _dataSchema;
    }
}
