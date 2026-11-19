// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// Metadata for a request stream as a whole.
    /// </summary>
    public class RequestStreamMetadata
    {
        /// <summary>
        /// The correlationId for tracking this streaming request
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// The Id of the client that invoked this streaming request
        /// </summary>
        public string? InvokerClientId { get; set; }

        /// <summary>
        /// The MQTT topic tokens used in this streaming request.
        /// </summary>
        public Dictionary<string, string> TopicTokens { get; } = new();

        /// <summary>
        /// The partition associated with this streaming request.
        /// </summary>
        public string? Partition { get; }

        /// <summary>
        /// The content type of all messages sent in this request stream.
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// The payload format indicator for all messages sent in this request stream.
        /// </summary>
        public MqttPayloadFormatIndicator PayloadFormatIndicator { get; set; }
    }
}
