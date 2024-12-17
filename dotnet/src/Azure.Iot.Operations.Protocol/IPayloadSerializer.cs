namespace Azure.Iot.Operations.Protocol
{
    public interface IPayloadSerializer
    {
        string ContentType { get; }

        int CharacterDataFormatIndicator { get; }

        byte[]? ToBytes<T>(T? payload) where T : class;

        T FromBytes<T>(byte[]? payload) where T : class;
    }
}
