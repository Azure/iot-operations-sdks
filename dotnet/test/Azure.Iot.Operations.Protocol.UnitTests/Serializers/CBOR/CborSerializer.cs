namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.CBOR
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Dahomey.Cbor;
    using Azure.Iot.Operations.Protocol;

#pragma warning disable VSTHRD002 // Synchronously waiting on tasks or awaiters may cause deadlocks. Use await or JoinableTaskFactory.Run instead.

    public class CborSerializer : IPayloadSerializer
    {
        protected static readonly CborOptions cborOptions = new()
        {
            DateTimeFormat = Dahomey.Cbor.DateTimeFormat.ISO8601,
            ObjectFormat = Dahomey.Cbor.Attributes.CborObjectFormat.IntKeyMap,
        };

        static CborSerializer()
        {
            cborOptions.Registry.ConverterRegistry.RegisterConverter(typeof(TimeSpan), new DurationCborConverter());
            cborOptions.Registry.ConverterRegistry.RegisterConverter(typeof(DateOnly), new DateCborConverter());
            cborOptions.Registry.ConverterRegistry.RegisterConverter(typeof(TimeOnly), new TimeCborConverter());
        }

        public string ContentType => "application/cbor";

        public int CharacterDataFormatIndicator => 0;

        public T FromBytes<T>(byte[]? payload)
            where T : class
        {
            try
            {
                if (payload == null)
                {
                    if (typeof(T) != typeof(EmptyCbor))
                    {
                        throw AkriMqttException.GetPayloadInvalidException();
                    }

                    return (new EmptyCbor() as T)!;
                }

                using (var stream = new MemoryStream(payload))
                {
                    ValueTask<T> task = Cbor.DeserializeAsync<T>(stream, cborOptions);
                    return task.IsCompletedSuccessfully ? task.Result : default!;
                }
            }
            catch (Exception)
            {
                throw AkriMqttException.GetPayloadInvalidException();
            }
        }

        public byte[]? ToBytes<T>(T? payload)
            where T : class
        {
            try
            {
                if (typeof(T) == typeof(EmptyCbor))
                {
                    return null;
                }

                using (var stream = new MemoryStream())
                {
                    Cbor.SerializeAsync(payload, stream, cborOptions).Wait();
                    stream.Flush();

                    byte[] buffer = new byte[stream.Length];
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.Read(buffer, 0, (int)stream.Length);

                    return buffer;
                }
            }
            catch (Exception)
            {
                throw AkriMqttException.GetPayloadInvalidException();
            }
        }
    }
}
