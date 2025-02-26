// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.AVRO;

namespace Azure.Iot.Operations.Protocol.UnitTests.Serialization
{
    public class AvroSerializerTests
    {
        [Fact]
        public void AvroUsesFormatIndicatorAsZero()
        {
            Assert.Equal(Models.MqttPayloadFormatIndicator.Unspecified, AvroSerializer<EmptyAvro, EmptyAvro>.PayloadFormatIndicator);
        }

        [Fact]
        public void DeserializeEmpty()
        {
            IPayloadSerializer avroSerializer = new AvroSerializer<EmptyAvro, EmptyAvro>();

            ReadOnlySequence<byte> nullBytes = avroSerializer.ToBytes(new EmptyAvro()).SerializedPayload;

            EmptyAvro? empty = avroSerializer.FromBytes<EmptyAvro>(nullBytes, null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.NotNull(empty);

            EmptyAvro? fromEmptyBytes = avroSerializer.FromBytes<EmptyAvro>(ReadOnlySequence<byte>.Empty, null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.NotNull(fromEmptyBytes);
        }

        [Fact]
        public void DeserializeNullToNonEmptyThrows()
        {
            IPayloadSerializer avroSerializer = new AvroSerializer<EmptyAvro, EmptyAvro>();

            Assert.Throws<AkriMqttException>(() => { avroSerializer.FromBytes<AvroCountTelemetry>(ReadOnlySequence<byte>.Empty, null, Models.MqttPayloadFormatIndicator.Unspecified); });
        }

        [Fact]
        public void FromTo_KnownType()
        {
            IPayloadSerializer avroSerializer = new AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>();
            var bytes = avroSerializer.ToBytes(new AvroCountTelemetry() { count = 2}).SerializedPayload;
            Assert.Equal(2, bytes.Length);

            AvroCountTelemetry fromBytes = avroSerializer.FromBytes<AvroCountTelemetry>(bytes, null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.Equal(2, fromBytes.count);

            byte[] newBytes = new byte[] { 0x02, 0x06 };
            AvroCountTelemetry fromNewBytes = avroSerializer.FromBytes<AvroCountTelemetry>(new(newBytes), null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.Equal(3, fromNewBytes.count);
        }

        [Fact]
        public void TypeWithNullValue()
        {
            IPayloadSerializer avroSerializer = new AvroSerializer<AvroCountTelemetry, AvroCountTelemetry>();
            var countTelemetry = new AvroCountTelemetry();
            Assert.Null(countTelemetry.count);
            var bytes = avroSerializer.ToBytes(countTelemetry).SerializedPayload;
            Assert.Equal(1, bytes.Length);

            AvroCountTelemetry fromBytes = avroSerializer.FromBytes<AvroCountTelemetry>(bytes, null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.NotNull(fromBytes);
            Assert.Null(fromBytes.count);

            AvroCountTelemetry fromBytesManual = avroSerializer.FromBytes<AvroCountTelemetry>(new(new byte[] {0x0}), null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.NotNull(fromBytesManual);
        }
    }
}
