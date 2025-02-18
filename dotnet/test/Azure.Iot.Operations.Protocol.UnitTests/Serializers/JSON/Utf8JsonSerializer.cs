﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.Models;

    public class Utf8JsonSerializer : IPayloadSerializer
    {
        protected static readonly JsonSerializerOptions jsonSerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new DurationJsonConverter(),
                new DateJsonConverter(),
                new TimeJsonConverter(),
                new UuidJsonConverter(),
                new BytesJsonConverter(),
                new DecimalJsonConverter(),
            }
        };

        public const string ContentType = "application/json";

        public const MqttPayloadFormatIndicator PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData;

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
                if (payload == null || payload.Length == 0)
                {
                    if (typeof(T) != typeof(EmptyJson))
                    {
                        throw AkriMqttException.GetPayloadInvalidException();
                    }

                    return (new EmptyJson() as T)!;
                }

                Utf8JsonReader reader = new(payload);
                return JsonSerializer.Deserialize<T>(ref reader, jsonSerializerOptions)!;
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
                if (typeof(T) == typeof(EmptyJson))
                {
                    return new(null, null, 0);
                }

                return new(JsonSerializer.SerializeToUtf8Bytes(payload, jsonSerializerOptions), ContentType, PayloadFormatIndicator);
            }
            catch (Exception)
            {
                throw AkriMqttException.GetPayloadInvalidException();
            }
        }
    }
}
