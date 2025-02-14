// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.custom
{
    public class CustomPayload : SerializedPayloadContext
    {
        public CustomPayload(byte[]? serializedPayload, string? contentType = "", MqttPayloadFormatIndicator payloadFormatIndicator = MqttPayloadFormatIndicator.Unspecified)
            : base(serializedPayload, contentType, payloadFormatIndicator)
        {
        }
    }
}
