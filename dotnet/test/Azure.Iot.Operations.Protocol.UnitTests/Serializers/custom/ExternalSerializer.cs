﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.custom
{
    using System;
    using System.Buffers;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Models;

    public class ExternalSerializer : IPayloadSerializer
    {
        public static readonly CustomPayload EmptyValue = new(ReadOnlySequence<byte>.Empty);

        public T FromBytes<T>(ReadOnlySequence<byte> payload, string? contentType, MqttPayloadFormatIndicator payloadFormatIndicator)
            where T : class
        {
            if (payload.IsEmpty)
            {
                return (Array.Empty<byte>() as T)!;
            }
            else if (typeof(T) == typeof(CustomPayload))
            {
                return (new CustomPayload(payload, contentType, payloadFormatIndicator) as T)!;
            }
            else
            {
                return default!;
            }
        }

        public SerializedPayloadContext ToBytes<T>(T? payload)
            where T : class
        {
            if (payload is CustomPayload payload1)
            {
                return payload1;
            }
            else
            {
                return EmptyValue;
            }
        }
    }
}
