// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
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

            ReadOnlySequence<byte> emptyBytes = rawSerializer.ToBytes<byte[]>(null).SerializedPayload;
            Assert.True(emptyBytes.IsEmpty);
            byte[] empty = rawSerializer.FromBytes<byte[]>(emptyBytes, null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.NotNull(empty);
            Assert.Empty(empty);
        }
    }
}
