// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol
{
    /// <summary>
    /// A bundle of the payload that was serialized alongside the content type and payload format indicator used.
    /// </summary>
    /// <param name="serializedPayload">The serialized payload. May be empty if no payload was serialized.</param>
    /// <param name="contentType">The content type of the serialized payload.  May be null if no payload was serialized.</param>
    /// <param name="payloadFormatIndicator">The payload format indicator of the serialized payload.  Should be "Unspecified" if no payload was serialized.</param>
    public class SerializedPayloadContext(ReadOnlySequence<byte> serializedPayload, string? contentType, MqttPayloadFormatIndicator payloadFormatIndicator)
    {
        /// <summary>
        /// The serialized payload. May be null if no payload was serialized.
        /// </summary>
        public ReadOnlySequence<byte> SerializedPayload { get; set; } = serializedPayload;

        /// <summary>
        /// The content type of the serialized payload.  May be null if no payload was serialized.
        /// </summary>
        public string? ContentType { get; set; } = contentType;

        /// <summary>
        /// The payload format indicator of the serialized payload.  Should be "Unspecified" if no payload was serialized.
        /// </summary>
        public MqttPayloadFormatIndicator PayloadFormatIndicator { get; set; } = payloadFormatIndicator;
    }
}
