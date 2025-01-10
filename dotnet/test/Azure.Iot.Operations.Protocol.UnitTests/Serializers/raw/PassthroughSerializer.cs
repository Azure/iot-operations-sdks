// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.raw
{
    using System;
    using Azure.Iot.Operations.Protocol;

    public class PassthroughSerializer : IPayloadSerializer
    {
        public string DefaultContentType => "application/octet-stream";

        public int DefaultPayloadFormatIndicator => 0;

        public DeserializedPayloadContext<T> FromBytes<T>(byte[]? payload, string? contentType, int? payloadFormatIndicator)
            where T : class
        {
            if (payload == null)
            {
                return new((Array.Empty<byte>() as T)!, contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
            }
            else if (typeof(T) == typeof(byte[]))
            {
                return new((payload as T)!, contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
            }
            else
            {
                return new(default!, contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
            }
        }

        public SerializedPayloadContext ToBytes<T>(T? payload, string? contentType, int? payloadFormatIndicator)
            where T : class
        {
            if (payload is byte[] payload1)
            {
                return new(payload1, contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
            }
            else
            {
                return new(Array.Empty<byte>(), contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
            }
        }
    }
}
