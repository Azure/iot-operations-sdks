// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;
using System.Buffers;
using System.Runtime.Serialization;

namespace Azure.Iot.Operations.Protocol.UnitTests.TestSerializers
{
    // Used for unit testing to simulate payload serialization/deserialization errors
    public class FaultySerializer : IPayloadSerializer
    {
        public const string ContentType = "application/json";
        public const MqttPayloadFormatIndicator PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData;
        public Type EmptyType { get => typeof(EmptyJson); }

        public T FromBytes<T>(ReadOnlySequence<byte> payload, string? contentType, MqttPayloadFormatIndicator payloadFormatIndicator)
            where T : class
        {
            throw new SerializationException();
        }

        public SerializedPayloadContext ToBytes<T>(T? payload)
            where T : class
        {
            throw new SerializationException();
        }
    }
}
