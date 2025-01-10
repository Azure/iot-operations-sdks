// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol
{
    public class DeserializedPayloadContext<T>(T deserializedPayload, string? contentType, int payloadFormatIndicator)
    {
        public T DeserializedPayload { get; set; } = deserializedPayload;

        public string? ContentType { get; set; } = contentType;

        public int PayloadFormatIndicator { get; set; } = payloadFormatIndicator;
    }
}
