// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.UnitTests.Serializers.CBOR;

namespace Azure.Iot.Operations.Protocol.UnitTests.Serialization
{
    public class MyCborType
    {
        [Dahomey.Cbor.Attributes.CborPropertyAttribute(index: 1)]
        public int MyIntProperty { get; set; }
        [Dahomey.Cbor.Attributes.CborPropertyAttribute(index: 2)]
        public string MyStringProperty { get; set; } = string.Empty;
    }

    public class CborSerializerTests
    {
        [Fact]
        public void CborUsesFormatIndicatorAsZero()
        {
            Assert.Equal(0, new CborSerializer().DefaultPayloadFormatIndicator);
        }

        [Fact]
        public void DeserializeEmpty()
        {
            IPayloadSerializer cborSerializer = new CborSerializer();

            SerializedPayloadContext emptyBytes = cborSerializer.ToBytes(new EmptyCbor(), null, 0);
            Assert.Null(emptyBytes.SerializedPayload);
            DeserializedPayloadContext<EmptyCbor> empty = cborSerializer.FromBytes<EmptyCbor>(emptyBytes.SerializedPayload, null, 0);
            Assert.NotNull(empty.DeserializedPayload);
        }

        [Fact]
        public void DeserializeNullToNonEmptyThrows()
        {
            IPayloadSerializer cborSerializer = new CborSerializer();

            Assert.Throws<AkriMqttException>(() => { cborSerializer.FromBytes<MyCborType>(null, null, 0); });
        }
    }
}
