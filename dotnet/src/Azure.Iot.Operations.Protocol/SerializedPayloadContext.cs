// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol
{
    public class SerializedPayloadContext(byte[]? serializedPayload, string? contentType, MqttPayloadFormatIndicator payloadFormatIndicator = MqttPayloadFormatIndicator.Unspecified)
    {
        public byte[]? SerializedPayload { get; set; } = serializedPayload;

        public string? ContentType { get; set; } = contentType;

        public MqttPayloadFormatIndicator PayloadFormatIndicator { get; set; } = payloadFormatIndicator;
    }
}