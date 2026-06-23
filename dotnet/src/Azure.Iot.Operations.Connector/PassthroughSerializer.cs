// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// A passthrough <see cref="IPayloadSerializer"/> for raw <c>byte[]</c> payloads. Used by
    /// connector primitives (telemetry sender, management action executor) that publish or
    /// receive opaque bytes and let the caller own content-type semantics.
    /// </summary>
    internal sealed class PassthroughSerializer : IPayloadSerializer
    {
        public const string ContentType = "application/octet-stream";

        public const MqttPayloadFormatIndicator PayloadFormatIndicator = MqttPayloadFormatIndicator.Unspecified;

        public T FromBytes<T>(ReadOnlySequence<byte> payload, string? contentType, MqttPayloadFormatIndicator payloadFormatIndicator)
            where T : class
        {
            if (payload.IsEmpty)
            {
                return (Array.Empty<byte>() as T)!;
            }

            if (typeof(T) == typeof(byte[]))
            {
                return (payload.ToArray() as T)!;
            }

            return null!;
        }

        public SerializedPayloadContext ToBytes<T>(T? payload)
            where T : class
        {
            if (payload is byte[] bytes)
            {
                return new SerializedPayloadContext(new ReadOnlySequence<byte>(bytes), ContentType, PayloadFormatIndicator);
            }

            return new SerializedPayloadContext(ReadOnlySequence<byte>.Empty, ContentType, PayloadFormatIndicator);
        }
    }
}
