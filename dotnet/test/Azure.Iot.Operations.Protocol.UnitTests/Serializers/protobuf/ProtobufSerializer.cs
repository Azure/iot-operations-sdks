// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

        public string DefaultContentType => "application/protobuf";

        public int DefaultPayloadFormatIndicator => 0;

        public DeserializedPayloadContext<T> FromBytes<T>(byte[]? payload, string? contentType, int? payloadFormatIndicator)
            where T : class
        {
            try
            {
                if (typeof(T) == typeof(T1))
                {
                    return new((messageParserT1.ParseFrom(payload ?? Array.Empty<byte>()) as T)!, contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
                }
                else if (typeof(T) == typeof(T2))
                {
                    return new((messageParserT2.ParseFrom(payload ?? Array.Empty<byte>()) as T)!, contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
                }
                else
                {
                    return new(default!, contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
                }
            }
            catch (Exception)
            {
                throw AkriMqttException.GetPayloadInvalidException();
            }
        }

        public SerializedPayloadContext ToBytes<T>(T? payload, string? contentType, int? payloadFormatIndicator)
            where T : class
        {
            try
            {
                if (typeof(T) == typeof(Empty))
                {
                    return new(null, contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
                }
                else if (typeof(T) == typeof(T1))
                {
                    return new((payload as IMessage<T1>).ToByteArray(), contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
                }
                else if (typeof(T) == typeof(T2))
                {
                    return new((payload as IMessage<T2>).ToByteArray(), contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
                }
                else
                {
                    return new(Array.Empty<byte>(), contentType ?? DefaultContentType, payloadFormatIndicator ?? DefaultPayloadFormatIndicator);
                }
            }
            catch (Exception)
            {
                throw AkriMqttException.GetPayloadInvalidException();
            }
        }
    }
}
