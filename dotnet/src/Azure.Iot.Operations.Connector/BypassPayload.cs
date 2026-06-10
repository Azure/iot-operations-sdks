// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// Raw bytes bundled with the content type and format indicator to emit on the wire.
    /// The command executor sources the response's content type from the serialized payload,
    /// so carrying it on the value lets a handler's chosen content type flow end-to-end.
    /// </summary>
    internal sealed class BypassPayload
    {
        public ReadOnlySequence<byte> Payload { get; init; } = ReadOnlySequence<byte>.Empty;

        public string? ContentType { get; init; }

        public MqttPayloadFormatIndicator FormatIndicator { get; init; } = MqttPayloadFormatIndicator.Unspecified;
    }

    /// <summary>
    /// Passthrough <see cref="IPayloadSerializer"/> for <see cref="BypassPayload"/>. Unlike
    /// <see cref="PassthroughSerializer"/> (which always tags bytes as
    /// <c>application/octet-stream</c>), this honors the content type and format indicator
    /// carried on each payload, so per-response content types are preserved.
    /// </summary>
    internal sealed class BypassSerializer : IPayloadSerializer
    {
        public const string DefaultContentType = "application/octet-stream";

        public SerializedPayloadContext ToBytes<T>(T? payload)
            where T : class
        {
            if (payload is BypassPayload bypass)
            {
                return new SerializedPayloadContext(
                    bypass.Payload,
                    bypass.ContentType ?? DefaultContentType,
                    bypass.FormatIndicator);
            }

            return new SerializedPayloadContext(
                ReadOnlySequence<byte>.Empty,
                DefaultContentType,
                MqttPayloadFormatIndicator.Unspecified);
        }

        public T FromBytes<T>(ReadOnlySequence<byte> payload, string? contentType, MqttPayloadFormatIndicator payloadFormatIndicator)
            where T : class
        {
            if (typeof(T) == typeof(BypassPayload))
            {
                return (new BypassPayload
                {
                    Payload = payload,
                    ContentType = contentType,
                    FormatIndicator = payloadFormatIndicator,
                } as T)!;
            }

            return null!;
        }
    }
}
