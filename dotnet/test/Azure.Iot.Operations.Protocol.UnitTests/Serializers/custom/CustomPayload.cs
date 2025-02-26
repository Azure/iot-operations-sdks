// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.custom
{
    public class CustomPayload : SerializedPayloadContext
    {
        public CustomPayload(ReadOnlySequence<byte> serializedPayload, string? contentType = "", MqttPayloadFormatIndicator payloadFormatIndicator = MqttPayloadFormatIndicator.Unspecified)
            : base(serializedPayload, contentType, payloadFormatIndicator)
        {
        }
    }
}
