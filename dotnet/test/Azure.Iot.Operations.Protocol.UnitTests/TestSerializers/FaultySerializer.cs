using Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON;
using System.Runtime.Serialization;

namespace Azure.Iot.Operations.Protocol.UnitTests.TestSerializers
{
    // Used for unit testing to simulate payload serialization/deserialization errors
    public class FaultySerializer : IPayloadSerializer
    {
        public string ContentType => "application/json";
        public int CharacterDataFormatIndicator => 1;
        public Type EmptyType { get => typeof(EmptyJson); }

        public T FromBytes<T>(byte[]? payload)
            where T : class
        {
            throw new SerializationException();
        }

        public byte[]? ToBytes<T>(T? payload)
            where T : class
        {
            throw new SerializationException();
        }
    }
}
