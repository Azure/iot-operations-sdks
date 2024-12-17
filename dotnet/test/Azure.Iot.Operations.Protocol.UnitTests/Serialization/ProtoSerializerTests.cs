using Google.Protobuf.WellKnownTypes;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.protobuf;

namespace Azure.Iot.Operations.Protocol.UnitTests.Serialization
{
    public class ProtoSerializerTests
    {

        [Fact]
        public void ProtoUsersFormatIndicatorZero()
        {
            Assert.Equal(0, new ProtobufSerializer<Empty,Empty>().CharacterDataFormatIndicator);
        }

        [Fact]
        public void DeserializeEmtpyAndNull()
        {
            IPayloadSerializer protobufSerializer = new ProtobufSerializer<Empty, Empty>();

            byte[]? nullBytes = protobufSerializer.ToBytes(new Empty());
            Assert.Null(nullBytes);
            Empty? empty = protobufSerializer.FromBytes<Empty>(nullBytes);
            Assert.NotNull(empty);

            Empty? empty2 = protobufSerializer.FromBytes<Empty>(Array.Empty<byte>());
            Assert.NotNull(empty2);
        }

        [Fact]
        public void DeserializeNullToNonEmptyDoesNotThrow()
        {
            IPayloadSerializer protobufSerializer = new ProtobufSerializer<ProtoCountTelemetry, ProtoCountTelemetry>();

            ProtoCountTelemetry protoCountTelemetry = protobufSerializer.FromBytes<ProtoCountTelemetry>(null);
            Assert.NotNull(protoCountTelemetry);
        }
    }
}
