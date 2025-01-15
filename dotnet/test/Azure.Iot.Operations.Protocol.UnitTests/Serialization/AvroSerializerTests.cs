// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.UnitTests.Serializers.AVRO;

namespace Azure.Iot.Operations.Protocol.UnitTests.Serialization
{
    public class AvroSerializerTests
    {
        [Fact]
        public void AvroUsesFormatIndicatorAsZero()
        {
            Assert.Equal(0, AvroSerializer<EmptyAvro, EmptyAvro>.DefaultPayloadFormatIndicator);
        }

        [Fact]
        public void DeserializeEmtpy()
        {
            var avroSerializer = new AvroSerializer<EmptyAvro, EmptyAvro>();

            byte[]? nullBytes = avroSerializer.ToBytes(new EmptyAvro()).SerializedPayload;
            Assert.Null(nullBytes);

            EmptyAvro? empty = avroSerializer.FromBytes<EmptyAvro>(nullBytes, AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>.DefaultContentType, AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>.DefaultPayloadFormatIndicator);
            Assert.NotNull(empty);

            EmptyAvro? fromEmptyBytes = avroSerializer.FromBytes<EmptyAvro>(Array.Empty<byte>(), AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>.DefaultContentType, AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>.DefaultPayloadFormatIndicator);
            Assert.NotNull(fromEmptyBytes);
        }

        [Fact]
        public void DeserializeNullToNonEmptyThrows()
        {
            var avroSerializer = new AvroSerializer<EmptyAvro, EmptyAvro>();

            Assert.Throws<AkriMqttException>(() => { avroSerializer.FromBytes<AvroCountTelemetry>(null, AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>.DefaultContentType, AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>.DefaultPayloadFormatIndicator); });
        }

        [Fact]
        public void FromTo_KnownType()
        {
            var avroSerializer = new AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>();
            var bytes = avroSerializer.ToBytes(new AvroCountTelemetry() { count = 2}).SerializedPayload;
            Assert.NotNull(bytes);
            Assert.Equal(2, bytes.Length);

            AvroCountTelemetry fromBytes = avroSerializer.FromBytes<AvroCountTelemetry>(bytes, AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>.DefaultContentType, AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>.DefaultPayloadFormatIndicator);
            Assert.Equal(2, fromBytes.count);

            byte[] newBytes = new byte[] { 0x02, 0x06 };
            AvroCountTelemetry fromNewBytes = avroSerializer.FromBytes<AvroCountTelemetry>(newBytes, AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>.DefaultContentType, AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>.DefaultPayloadFormatIndicator);
            Assert.Equal(3, fromNewBytes.count);
        }

        [Fact]
        public void TypeWithNullValue()
        {
            var avroSerializer = new AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>();
            var countTelemetry = new AvroCountTelemetry();
            Assert.Null(countTelemetry.count);
            var bytes = avroSerializer.ToBytes(countTelemetry).SerializedPayload;
            Assert.Single(bytes!);

            AvroCountTelemetry fromBytes = avroSerializer.FromBytes<AvroCountTelemetry>(bytes, AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>.DefaultContentType, AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>.DefaultPayloadFormatIndicator);
            Assert.NotNull(fromBytes);
            Assert.Null(fromBytes.count);

            AvroCountTelemetry fromBytesManual = avroSerializer.FromBytes<AvroCountTelemetry>(new byte[] {0x0}, AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>.DefaultContentType, AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>.DefaultPayloadFormatIndicator);
            Assert.NotNull(fromBytesManual);
        }
    }
}
