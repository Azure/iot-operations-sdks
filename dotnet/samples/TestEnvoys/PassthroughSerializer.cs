namespace TestEnvoys
{
    using System;
    using Azure.Iot.Operations.Protocol;

    public class PassthroughSerializer : IPayloadSerializer
    {
        public string ContentType => "application/octet-stream";

        public int CharacterDataFormatIndicator => 0;

        public T FromBytes<T>(byte[]? payload)
            where T : class
        {
            if (payload == null)
            {
                return (Array.Empty<byte>() as T)!;
            }
            else if (typeof(T) == typeof(byte[]))
            {
                return (payload as T)!;
            }
            else
            {
                return default!;
            }
        }

        public byte[]? ToBytes<T>(T? payload)
            where T : class
        {
            if (payload is byte[] payload1)
            {
                return payload1;
            }
            else
            {
                return Array.Empty<byte>();
            }
        }
    }
}
