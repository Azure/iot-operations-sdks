// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol
{
    /// <summary>
    /// CloudEvent implementation for use with Azure IoT Operations protocol libraries (Telemetry and RPC).
    /// This class is designed for protocol-specific scenarios where the Type and DataContentType are
    /// controlled by the SDK rather than by user code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike the generic <see cref="CloudEvent"/>, this class:
    /// - Has a default Type that is set by the protocol (e.g., "ms.aio.telemetry", "ms.aio.rpc.request")
    /// - Has DataContentType automatically set from the payload serializer
    /// - Does not require Type to be specified in the constructor
    /// </para>
    /// <para>
    /// For generic CloudEvent usage with MQTT messages outside of protocol contexts,
    /// use <see cref="CloudEvent"/> instead.
    /// </para>
    /// See <a href="https://github.com/cloudevents/spec/blob/main/cloudevents/spec.md">CloudEvent Spec</a>
    /// </remarks>
    /// <param name="source">The source URI identifying where the event originated</param>
    /// <param name="specversion">The CloudEvents specification version (defaults to "1.0")</param>
    public class ProtocolCloudEvent(Uri source, string specversion = "1.0")
    {
        /// <summary>
        /// Identifies the context in which an event happened.
        /// Often this will include information such as the type of the event source,
        /// the organization publishing the event or the process that produced the event.
        /// The exact syntax and semantics behind the data encoded in the URI is defined by the event producer.
        /// </summary>
        public Uri Source => source;

        /// <summary>
        /// The version of the CloudEvents specification which the event uses.
        /// This enables the interpretation of the context.
        /// Compliant event producers MUST use a value of 1.0 when referring to this version of the specification.
        /// </summary>
        public string SpecVersion => specversion;

        /// <summary>
        /// Contains a value describing the type of event related to the originating occurrence.
        /// This value is automatically set by the protocol library based on context:
        /// - "ms.aio.telemetry" for telemetry messages
        /// - "ms.aio.rpc.request" for RPC command requests
        /// - "ms.aio.rpc.response" for RPC command responses
        /// </summary>
        /// <remarks>
        /// This property is set internally by the SDK and should not be modified by user code.
        /// </remarks>
        public string Type { get; internal set; } = "ms.aio.telemetry";

        /// <summary>
        /// Identifies the event. Producers MUST ensure that source + id is unique for each distinct event.
        /// If a duplicate event is re-sent (e.g. due to a network error) it MAY have the same id.
        /// Consumers MAY assume that Events with identical source and id are duplicates.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Timestamp of when the occurrence happened.
        /// If the time of the occurrence cannot be determined then this attribute MAY be set to some other time
        /// (such as the current time) by the CloudEvents producer, however all producers for the same source
        /// MUST be consistent in this respect.
        /// </summary>
        public DateTime? Time { get; set; }

        /// <summary>
        /// Content type of data value. This attribute enables data to carry any type of content,
        /// whereby format and encoding might differ from that of the chosen event format.
        /// </summary>
        /// <remarks>
        /// This property is automatically set by the protocol library from the payload serializer
        /// and should not be modified by user code. The value comes from the SerializedPayloadContext.ContentType.
        /// </remarks>
        public string? DataContentType { get; internal set; }

        /// <summary>
        /// Identifies the subject of the event in the context of the event producer (identified by source).
        /// In publish-subscribe scenarios, a subscriber will typically subscribe to events emitted by a source,
        /// but the source identifier alone might not be sufficient as a qualifier for any specific event
        /// if the source context has internal sub-structure.
        /// </summary>
        public string? Subject { get; set; }

        /// <summary>
        /// Identifies the schema that data adheres to.
        /// Incompatible changes to the schema SHOULD be reflected by a different URI.
        /// </summary>
        public string? DataSchema { get; set; }
    }
}
