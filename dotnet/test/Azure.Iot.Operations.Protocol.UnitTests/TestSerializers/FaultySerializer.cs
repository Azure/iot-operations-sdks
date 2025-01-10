// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;
using System.Runtime.Serialization;

namespace Azure.Iot.Operations.Protocol.UnitTests.TestSerializers
{
    // Used for unit testing to simulate payload serialization/deserialization errors
    public class FaultySerializer : IPayloadSerializer
    {
        public string DefaultContentType => "application/json";
        public int DefaultPayloadFormatIndicator => 1;
        public Type EmptyType { get => typeof(EmptyJson); }
        
        public T FromBytes<T>(byte[]? payload, string? contentType, int? payloadFormatIndicator) where T : class
        {
            throw new SerializationException();
        }

        public SerializedPayloadContext ToBytes<T>(T? payload, string? contentType, int? payloadFormatIndicator) where T : class
        {
            throw new SerializationException();
        }
    }
}
