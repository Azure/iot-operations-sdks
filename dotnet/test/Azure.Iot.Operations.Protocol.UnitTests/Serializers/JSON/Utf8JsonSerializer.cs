﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* This file will be copied into the folder for generated code. */

namespace Azure.Iot.Operations.Protocol.UnitTests.Serializers.JSON
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Iot.Operations.Protocol;

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

        public string DefaultContentType => "application/json";

        public int DefaultPayloadFormatIndicator => 1;

        public T FromBytes<T>(byte[]? payload, string? contentType, int? payloadFormatIndicator)
            where T : class
        {
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
                    return new(null, DefaultContentType, DefaultPayloadFormatIndicator);
                }

                return new(JsonSerializer.SerializeToUtf8Bytes(payload, jsonSerializerOptions), DefaultContentType, DefaultPayloadFormatIndicator);
            }
            catch (Exception)
            {
                throw AkriMqttException.GetPayloadInvalidException();
            }
        }
    }
}
