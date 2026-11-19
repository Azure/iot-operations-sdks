// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// Metadata for a response stream as a whole.
    /// </summary>
    public class ResponseStreamMetadata
    {
        /// <summary>
        /// The content type of all messages in this response stream
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// The payload format indicator for all messages in this response stream
        /// </summary>
        public MqttPayloadFormatIndicator PayloadFormatIndicator { get; set; }
    }
}
