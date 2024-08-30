using Azure.Iot.Operations.Protocol.UnitTests.Serializers.AVRO;

namespace Azure.Iot.Operations.Protocol.UnitTests.Serialization
{
    public class AvroSerializerTests
    {
        [Fact]
        public void AvroUsesFormatIndicatorAsZero()
        {
            Assert.Equal(0, new AvroSerializer<EmptyAvro, EmptyAvro>().CharacterDataFormatIndicator);
        }

        [Fact]
        public void DeserializeEmtpy()
        {
            IPayloadSerializer avroSerializer = new AvroSerializer<EmptyAvro, EmptyAvro>();

            byte[]? nullBytes = avroSerializer.ToBytes(new EmptyAvro());
            Assert.Null(nullBytes);

            EmptyAvro? empty = avroSerializer.FromBytes<EmptyAvro>(nullBytes);
            Assert.NotNull(empty);

            EmptyAvro? fromEmptyBytes = avroSerializer.FromBytes<EmptyAvro>(Array.Empty<byte>());
            Assert.NotNull(fromEmptyBytes);
        }

        [Fact]
        public void DeserializeNullToNonEmptyThrows()
        {
            IPayloadSerializer avroSerializer = new AvroSerializer<EmptyAvro, EmptyAvro>();

            Assert.Throws<AkriMqttException>(() => { avroSerializer.FromBytes<AvroCountTelemetry>(null); });
        }

        [Fact]
        public void FromTo_KnownType()
        {
            IPayloadSerializer avroSerializer = new AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>();
            var bytes = avroSerializer.ToBytes(new AvroCountTelemetry() { count = 2});
            Assert.NotNull(bytes);
            Assert.Equal(2, bytes.Length);

            AvroCountTelemetry fromBytes = avroSerializer.FromBytes<AvroCountTelemetry>(bytes);
            Assert.Equal(2, fromBytes.count);

            byte[] newBytes = new byte[] { 0x02, 0x06 };
            AvroCountTelemetry fromNewBytes = avroSerializer.FromBytes<AvroCountTelemetry>(newBytes);
            Assert.Equal(3, fromNewBytes.count);
        }

        [Fact]
        public void TypeWithNullValue()
        {
            IPayloadSerializer avroSerializer = new AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>();
            var countTelemetry = new AvroCountTelemetry();
            Assert.Null(countTelemetry.count);
            var bytes = avroSerializer.ToBytes(countTelemetry);
            Assert.Single(bytes!);

            AvroCountTelemetry fromBytes = avroSerializer.FromBytes<AvroCountTelemetry>(bytes);
            Assert.NotNull(fromBytes);
            Assert.Null(fromBytes.count);

            AvroCountTelemetry fromBytesManual = avroSerializer.FromBytes<AvroCountTelemetry>(new byte[] {0x0});
            Assert.NotNull(fromBytesManual);
        }
    }
}
