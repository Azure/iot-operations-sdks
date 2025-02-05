// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.custom
{
    using System;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Models;

    public class ExternalSerializer : IPayloadSerializer
    {
        public static readonly CustomPayload EmptyValue = new(Array.Empty<byte>());

        public T FromBytes<T>(byte[]? payload, string? contentType, MqttPayloadFormatIndicator payloadFormatIndicator)
            where T : class
        {
            if (payload == null)
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
