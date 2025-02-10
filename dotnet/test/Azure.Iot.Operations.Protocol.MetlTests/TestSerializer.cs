// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    using System;
    using System.Text;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Models;

    public class TestSerializer : IPayloadSerializer
    {
        private string? outContentType;
        private List<string> acceptContentTypes;
        private MqttPayloadFormatIndicator outPayloadFormat;
        private bool allowCharacterData;
        private bool failDeserialization;

        public TestSerializer(TestCaseSerializer testCaseSerializer)
        {
            this.outContentType = testCaseSerializer.OutContentType;
            this.acceptContentTypes = testCaseSerializer.AcceptContentTypes;
            this.outPayloadFormat = testCaseSerializer.IndicateCharacterData ? MqttPayloadFormatIndicator.CharacterData : MqttPayloadFormatIndicator.Unspecified;
            this.allowCharacterData = testCaseSerializer.AllowCharacterData;
            this.failDeserialization = testCaseSerializer.FailDeserialization;
        }

        public T FromBytes<T>(byte[]? payload, string? contentType, MqttPayloadFormatIndicator payloadFormatIndicator)
            where T : class
        {
            if (contentType != null && !acceptContentTypes.Contains(contentType))
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

            if (payloadFormatIndicator == MqttPayloadFormatIndicator.CharacterData && !allowCharacterData)
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

            if (failDeserialization)
            {
                throw AkriMqttException.GetPayloadInvalidException();
            }

            if (typeof(T) == typeof(string))
            {
                if (payload == null)
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
                    return new(null, null, MqttPayloadFormatIndicator.Unspecified);
                }
                else if (payloadString.Length == 0)
                {
                    return new(Array.Empty<byte>(), outContentType, outPayloadFormat);
                }
                else
                {
                    return new(Encoding.UTF8.GetBytes(payloadString), outContentType, outPayloadFormat);
                }
            }
            else
            {
                return new(null, null, MqttPayloadFormatIndicator.Unspecified);
            }
        }
    }
}
