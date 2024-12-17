/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.protobuf
{
    using System;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;
    using Azure.Iot.Operations.Protocol;

    public class ProtobufSerializer<T1, T2> : IPayloadSerializer
        where T1 : IMessage<T1>, new()
        where T2 : IMessage<T2>, new()
    {
        private readonly MessageParser<T1> messageParserT1;
        private readonly MessageParser<T2> messageParserT2;

        public ProtobufSerializer()
        {
            messageParserT1 = new MessageParser<T1>(() => new T1());
            messageParserT2 = new MessageParser<T2>(() => new T2());
        }

        public string ContentType => "application/protobuf";

        public int CharacterDataFormatIndicator => 0;

        public T FromBytes<T>(byte[]? payload)
            where T : class
        {
            try
            {
                if (typeof(T) == typeof(T1))
                {
                    return (messageParserT1.ParseFrom(payload ?? Array.Empty<byte>()) as T)!;
                }
                else if (typeof(T) == typeof(T2))
                {
                    return (messageParserT2.ParseFrom(payload ?? Array.Empty<byte>()) as T)!;
                }
                else
                {
                    return default!;
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
                if (typeof(T) == typeof(Empty))
                {
                    return null;
                }
                else if (typeof(T) == typeof(T1))
                {
                    return (payload as IMessage<T1>).ToByteArray();
                }
                else if (typeof(T) == typeof(T2))
                {
                    return (payload as IMessage<T2>).ToByteArray();
                }
                else
                {
                    return Array.Empty<byte>();
                }
            }
            catch (Exception)
            {
                throw AkriMqttException.GetPayloadInvalidException();
            }
        }
    }
}
