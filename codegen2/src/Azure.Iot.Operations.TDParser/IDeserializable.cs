namespace Azure.Iot.Operations.TDParser
{
    using System.Text.Json;

    public interface IDeserializable<T> : ITraversable
        where T : IDeserializable<T>
    {
        static abstract T Deserialize(ref Utf8JsonReader reader);
    }
}
