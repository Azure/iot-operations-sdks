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
            Assert.Equal(Models.MqttPayloadFormatIndicator.Unspecified, PassthroughSerializer.PayloadFormatIndicator);
        }

        [Fact]
        public void DeserializeEmpty()
        {
            IPayloadSerializer rawSerializer = new PassthroughSerializer();

            byte[]? emptyBytes = rawSerializer.ToBytes<byte[]>(null).SerializedPayload;
            Assert.NotNull(emptyBytes);
            Assert.Empty(emptyBytes);
            byte[] empty = rawSerializer.FromBytes<byte[]>(emptyBytes, null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.NotNull(empty);
            Assert.Empty(empty);
        }
    }
}
