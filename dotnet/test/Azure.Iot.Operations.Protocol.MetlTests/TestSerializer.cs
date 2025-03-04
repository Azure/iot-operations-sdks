// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System;
    using System.Buffers;
    using System.Text;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Models;

    public class TestSerializer : IPayloadSerializer
    {
        private readonly string? _outContentType;
        private readonly List<string> _acceptContentTypes;
        private readonly MqttPayloadFormatIndicator _outPayloadFormat;
        private readonly bool _allowCharacterData;
        private readonly bool _failDeserialization;

        public TestSerializer(TestCaseSerializer testCaseSerializer)
        {
            _outContentType = testCaseSerializer.OutContentType;
            _acceptContentTypes = testCaseSerializer.AcceptContentTypes;
            _outPayloadFormat = testCaseSerializer.IndicateCharacterData ? MqttPayloadFormatIndicator.CharacterData : MqttPayloadFormatIndicator.Unspecified;
            _allowCharacterData = testCaseSerializer.AllowCharacterData;
            _failDeserialization = testCaseSerializer.FailDeserialization;
        }

        public T FromBytes<T>(ReadOnlySequence<byte> payload, string? contentType, MqttPayloadFormatIndicator payloadFormatIndicator)
            where T : class
        {
            if (contentType != null && !_acceptContentTypes.Contains(contentType))
            {
                throw new AkriMqttException($"Content type {contentType} is not allowed.")
                {
                    Kind = AkriMqttErrorKind.HeaderInvalid,
                    HeaderName = "Content Type",
                    HeaderValue = contentType,
                    InApplication = false,
                    IsShallow = false,
                    IsRemote = false,
                };
            }

            if (payloadFormatIndicator == MqttPayloadFormatIndicator.CharacterData && !_allowCharacterData)
            {
                throw new AkriMqttException($"Character data format indicator is not allowed.")
                {
                    Kind = AkriMqttErrorKind.HeaderInvalid,
                    HeaderName = "Payload Format Indicator",
                    HeaderValue = "CharacterData",
                    InApplication = false,
                    IsShallow = false,
                    IsRemote = false,
                };
            }

            if (_failDeserialization)
            {
                throw AkriMqttException.GetPayloadInvalidException();
            }

            if (typeof(T) == typeof(string))
            {
                if (payload.IsEmpty)
                {
                    return (null as T)!;
                }
                else if (payload.Length == 0)
                {
                    return (string.Empty as T)!;
                }
                else
                {
                    return (Encoding.UTF8.GetString(payload) as T)!;
                }
            }
            else
            {
                return default!;
            }
        }

        public SerializedPayloadContext ToBytes<T>(T? payload)
            where T : class
        {
            if (typeof(T) == typeof(string))
            {
                string? payloadString = payload as string;

                if (payloadString == null)
                {
                    return new(ReadOnlySequence<byte>.Empty, null, MqttPayloadFormatIndicator.Unspecified);
                }
                else if (payloadString.Length == 0)
                {
                    return new(ReadOnlySequence<byte>.Empty, _outContentType, _outPayloadFormat);
                }
                else
                {
                    return new(new(Encoding.UTF8.GetBytes(payloadString)), _outContentType, _outPayloadFormat);
                }
            }
            else
            {
                return new(ReadOnlySequence<byte>.Empty, null, MqttPayloadFormatIndicator.Unspecified);
            }
        }
    }
}
