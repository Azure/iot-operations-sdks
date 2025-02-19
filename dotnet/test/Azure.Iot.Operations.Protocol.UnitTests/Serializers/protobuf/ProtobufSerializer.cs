// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.protobuf
{
    using System;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Models;

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

        public const string ContentType = "application/protobuf";

        public const MqttPayloadFormatIndicator PayloadFormatIndicator = MqttPayloadFormatIndicator.Unspecified;

        public T FromBytes<T>(byte[]? payload, string? contentType, MqttPayloadFormatIndicator payloadFormatIndicator)
            where T : class
        {
            if (contentType != null && contentType != ContentType)
            {
                throw new AkriMqttException($"Content type {contentType} is not supported by this implementation; only {ContentType} is accepted.")
                {
                    Kind = AkriMqttErrorKind.HeaderInvalid,
                    HeaderName = "Content Type",
                    HeaderValue = contentType,
                    InApplication = false,
                    IsShallow = false,
                    IsRemote = false,
                };
            }

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

        public SerializedPayloadContext ToBytes<T>(T? payload)
            where T : class
        {
            try
            {
                if (typeof(T) == typeof(Empty))
                {
                    return new(null, null, 0);
                }
                else if (typeof(T) == typeof(T1))
                {
                    return new((payload as IMessage<T1>).ToByteArray(), ContentType, PayloadFormatIndicator);
                }
                else if (typeof(T) == typeof(T2))
                {
                    return new((payload as IMessage<T2>).ToByteArray(), ContentType, PayloadFormatIndicator);
                }
                else
                {
                    return new(Array.Empty<byte>(), ContentType, PayloadFormatIndicator);
                }
            }
            catch (Exception)
            {
                throw AkriMqttException.GetPayloadInvalidException();
            }
        }
    }
}
