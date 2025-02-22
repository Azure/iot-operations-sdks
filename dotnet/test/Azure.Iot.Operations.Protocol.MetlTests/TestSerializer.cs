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
        private string? _outContentType;
        private List<string> _acceptContentTypes;
        private MqttPayloadFormatIndicator _outPayloadFormat;
        private bool _allowCharacterData;
        private bool _failDeserialization;

        public TestSerializer(TestCaseSerializer testCaseSerializer)
        {
            this._outContentType = testCaseSerializer.OutContentType;
            this._acceptContentTypes = testCaseSerializer.AcceptContentTypes;
            this._outPayloadFormat = testCaseSerializer.IndicateCharacterData ? MqttPayloadFormatIndicator.CharacterData : MqttPayloadFormatIndicator.Unspecified;
            this._allowCharacterData = testCaseSerializer.AllowCharacterData;
            this._failDeserialization = testCaseSerializer.FailDeserialization;
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
