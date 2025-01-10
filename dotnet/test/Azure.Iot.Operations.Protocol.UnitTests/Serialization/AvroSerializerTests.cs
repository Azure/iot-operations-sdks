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
            Assert.Equal(0, new AvroSerializer<EmptyAvro, EmptyAvro>().DefaultPayloadFormatIndicator);
        }

        [Fact]
        public void DeserializeEmtpy()
        {
            IPayloadSerializer avroSerializer = new AvroSerializer<EmptyAvro, EmptyAvro>();

            byte[]? nullBytes = avroSerializer.ToBytes(new EmptyAvro(),avroSerializer.DefaultContentType,avroSerializer.DefaultPayloadFormatIndicator).SerializedPayload;
            Assert.Null(nullBytes);

            EmptyAvro? empty = avroSerializer.FromBytes<EmptyAvro>(nullBytes,avroSerializer.DefaultContentType,avroSerializer.DefaultPayloadFormatIndicator);
            Assert.NotNull(empty);

            EmptyAvro? fromEmptyBytes = avroSerializer.FromBytes<EmptyAvro>(Array.Empty<byte>(),avroSerializer.DefaultContentType,avroSerializer.DefaultPayloadFormatIndicator);
            Assert.NotNull(fromEmptyBytes);
        }

        [Fact]
        public void DeserializeNullToNonEmptyThrows()
        {
            IPayloadSerializer avroSerializer = new AvroSerializer<EmptyAvro, EmptyAvro>();

            Assert.Throws<AkriMqttException>(() => { avroSerializer.FromBytes<AvroCountTelemetry>(null,avroSerializer.DefaultContentType,avroSerializer.DefaultPayloadFormatIndicator); });
        }

        [Fact]
        public void FromTo_KnownType()
        {
            IPayloadSerializer avroSerializer = new AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>();
            var bytes = avroSerializer.ToBytes(new AvroCountTelemetry() { count = 2},avroSerializer.DefaultContentType,avroSerializer.DefaultPayloadFormatIndicator).SerializedPayload;
            Assert.NotNull(bytes);
            Assert.Equal(2, bytes.Length);

            AvroCountTelemetry fromBytes = avroSerializer.FromBytes<AvroCountTelemetry>(bytes,avroSerializer.DefaultContentType,avroSerializer.DefaultPayloadFormatIndicator);
            Assert.Equal(2, fromBytes.count);

            byte[] newBytes = new byte[] { 0x02, 0x06 };
            AvroCountTelemetry fromNewBytes = avroSerializer.FromBytes<AvroCountTelemetry>(newBytes,avroSerializer.DefaultContentType,avroSerializer.DefaultPayloadFormatIndicator);
            Assert.Equal(3, fromNewBytes.count);
        }

        [Fact]
        public void TypeWithNullValue()
        {
            IPayloadSerializer avroSerializer = new AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>();
            var countTelemetry = new AvroCountTelemetry();
            Assert.Null(countTelemetry.count);
            var bytes = avroSerializer.ToBytes(countTelemetry,avroSerializer.DefaultContentType,avroSerializer.DefaultPayloadFormatIndicator).SerializedPayload;
            Assert.Single(bytes!);

            AvroCountTelemetry fromBytes = avroSerializer.FromBytes<AvroCountTelemetry>(bytes,avroSerializer.DefaultContentType,avroSerializer.DefaultPayloadFormatIndicator);
            Assert.NotNull(fromBytes);
            Assert.Null(fromBytes.count);

            AvroCountTelemetry fromBytesManual = avroSerializer.FromBytes<AvroCountTelemetry>(new byte[] {0x0},avroSerializer.DefaultContentType,avroSerializer.DefaultPayloadFormatIndicator);
            Assert.NotNull(fromBytesManual);
        }
    }
}
