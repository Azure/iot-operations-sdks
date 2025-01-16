// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol
{
    public class SerializedPayloadContext(byte[]? serializedPayload, string? contentType, int payloadFormatIndicator)
    {
        public byte[]? SerializedPayload { get; set; } = serializedPayload;

        public string? ContentType { get; set; } = contentType;

        public int PayloadFormatIndicator { get; set; } = payloadFormatIndicator;
    }
}