// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;

namespace EventDrivenApp;

public class EmptyJson
{
}

public class Utf8JsonSerializer : IPayloadSerializer
{
    protected static readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public const string ContentType = "application/json";

    public const MqttPayloadFormatIndicator PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData;

    public T FromBytes<T>(ReadOnlySequence<byte> payload, string? contentType, MqttPayloadFormatIndicator payloadFormatIndicator)
        where T : class
    {
        if (contentType != null && contentType != ContentType)
        {
            throw new AkriMqttException($"Content type {contentType} is not supported by this implementation; only {ContentType} is accepted.")
            {
                Kind = AkriMqttErrorKind.HeaderInvalid,
                HeaderName = "Content Type",
                HeaderValue = contentType,
                IsShallow = false,
                IsRemote = false,
            };
        }

        try
        {
            if (payload.IsEmpty)
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
                return new(ReadOnlySequence<byte>.Empty, ContentType, PayloadFormatIndicator);
            }

            return new(new(JsonSerializer.SerializeToUtf8Bytes(payload, jsonSerializerOptions)), ContentType, PayloadFormatIndicator);
        }
        catch (Exception)
        {
            throw AkriMqttException.GetPayloadInvalidException();
        }
    }
}
