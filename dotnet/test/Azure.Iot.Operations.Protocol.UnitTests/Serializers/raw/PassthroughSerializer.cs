﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.raw
{
    using System;
    using System.Buffers;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Models;

    public class PassthroughSerializer : IPayloadSerializer
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
                return new(new(payload1), ContentType, PayloadFormatIndicator);
            }
            else
            {
                return new(ReadOnlySequence<byte>.Empty, ContentType, PayloadFormatIndicator);
            }
        }
    }
}
