// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Google.Protobuf.WellKnownTypes;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.protobuf;
using System.Buffers;

namespace Azure.Iot.Operations.Protocol.UnitTests.Serialization
{
    public class ProtoSerializerTests
    {

        [Fact]
        public void ProtoUsersFormatIndicatorZero()
        {
            Assert.Equal(Models.MqttPayloadFormatIndicator.Unspecified, ProtobufSerializer<Empty,Empty>.PayloadFormatIndicator);
        }

        [Fact]
        public void DeserializeEmptyAndNull()
        {
            IPayloadSerializer protobufSerializer = new ProtobufSerializer<Empty, Empty>();

            ReadOnlySequence<byte> nullBytes = protobufSerializer.ToBytes(new Empty()).SerializedPayload;
            Assert.True(nullBytes.IsEmpty);
            Empty? empty = protobufSerializer.FromBytes<Empty>(nullBytes, null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.NotNull(empty);

            Empty? empty2 = protobufSerializer.FromBytes<Empty>(ReadOnlySequence<byte>.Empty, null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.NotNull(empty2);
        }

        [Fact]
        public void DeserializeNullToNonEmptyDoesNotThrow()
        {
            IPayloadSerializer protobufSerializer = new ProtobufSerializer<ProtoCountTelemetry, ProtoCountTelemetry>();

            ProtoCountTelemetry protoCountTelemetry = protobufSerializer.FromBytes<ProtoCountTelemetry>(ReadOnlySequence<byte>.Empty, null, Models.MqttPayloadFormatIndicator.Unspecified);
            Assert.NotNull(protoCountTelemetry);
        }
    }
}
