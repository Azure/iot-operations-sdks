﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.UnitTests.Serializers.raw;

namespace Azure.Iot.Operations.Protocol.UnitTests.Serialization
{
    public class PassthroughSerializerTests
    {
        [Fact]
        public void PassthroughUsesFormatIndicatorAsZero()
        {
            Assert.Equal(0, PassthroughSerializer.PayloadFormatIndicator);
        }

        [Fact]
        public void DeserializeEmpty()
        {
            IPayloadSerializer rawSerializer = new PassthroughSerializer();

            byte[]? emptyBytes = rawSerializer.ToBytes<byte[]>(null).SerializedPayload;
            Assert.NotNull(emptyBytes);
            Assert.Empty(emptyBytes);
            byte[] empty = rawSerializer.FromBytes<byte[]>(emptyBytes);
            Assert.NotNull(empty);
            Assert.Empty(empty);
        }
    }
}
