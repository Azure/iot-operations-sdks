// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */

namespace TestEnvoys
{
    using System;
    using Azure.Iot.Operations.Protocol;

    public class PassthroughSerializer : IPayloadSerializer
    {
        public const string DefaultContentType = "application/octet-stream";

        public const int DefaultPayloadFormatIndicator = 0;

        public T FromBytes<T>(byte[]? payload, string? contentType = null, int? payloadFormatIndicator = null)
            where T : class
        {
            if (payload == null)
            {
                return (Array.Empty<byte>() as T)!;
            }
            else if (typeof(T) == typeof(byte[]))
            {
                return (payload as T)!;
            }
            else
            {
                return default!;
            }
        }

        public SerializedPayloadContext ToBytes<T>(T? payload)
            where T : class
        {
            if (payload is byte[] payload1)
            {
                return new(payload1, DefaultContentType, DefaultPayloadFormatIndicator);
            }
            else
            {
                return new(Array.Empty<byte>(), DefaultContentType, DefaultPayloadFormatIndicator);
            }
        }
    }
}
