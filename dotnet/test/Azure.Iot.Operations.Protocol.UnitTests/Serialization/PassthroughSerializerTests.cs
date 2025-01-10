// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.UnitTests.Serializers.raw;

namespace Azure.Iot.Operations.Protocol.UnitTests.Serialization
{
    public class PassthroughSerializerTests
    {
        [Fact]
        public void PassthroughUsesFormatIndicatorAsZero()
        {
            Assert.Equal(0, new PassthroughSerializer().DefaultPayloadFormatIndicator);
        }

        [Fact]
        public void DeserializeEmpty()
        {
            IPayloadSerializer rawSerializer = new PassthroughSerializer();

            byte[]? emptyBytes = rawSerializer.ToBytes<byte[]>(null, rawSerializer.DefaultContentType, rawSerializer.DefaultPayloadFormatIndicator).SerializedPayload;
            Assert.NotNull(emptyBytes);
            Assert.Empty(emptyBytes);
            byte[] empty = rawSerializer.FromBytes<byte[]>(emptyBytes, rawSerializer.DefaultContentType, rawSerializer.DefaultPayloadFormatIndicator);
            Assert.NotNull(empty);
            Assert.Empty(empty);
        }
    }
}
