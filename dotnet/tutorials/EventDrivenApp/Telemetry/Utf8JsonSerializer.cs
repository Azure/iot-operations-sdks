// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Iot.Operations.Protocol;

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

    public string ContentType => "application/json";

    public bool IsContentTypeSupersedable => false;

    public int CharacterDataFormatIndicator => 1;

    public T FromBytes<T>(byte[]? payload)
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

    public byte[]? ToBytes<T>(T? payload)
        where T : class
    {
        try
        {
            if (typeof(T) == typeof(EmptyJson))
            {
                return null;
            }

            return JsonSerializer.SerializeToUtf8Bytes(payload, jsonSerializerOptions);
        }
        catch (Exception)
        {
            throw AkriMqttException.GetPayloadInvalidException();
        }
    }
}
