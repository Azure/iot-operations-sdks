// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Azure.Iot.Operations.Protocol.RPC;
using TestEnvoys;

namespace SampleServer
{
    public class ErrorPayloadJsonSerializer : IErrorHeaderPayloadSerializer
    {
        private readonly Utf8JsonSerializer _jsonSerializer;

        public ErrorPayloadJsonSerializer(Utf8JsonSerializer jsonSerializer)
        {
            _jsonSerializer = jsonSerializer;
        }

        public T FromString<T>(string payloadString) where T : class
        {
            return _jsonSerializer.FromBytes<T>(new (Encoding.UTF8.GetBytes(payloadString)), null, Azure.Iot.Operations.Protocol.Models.MqttPayloadFormatIndicator.CharacterData);
        }

        public string ToString<T>(T? payload) where T : class
        {
            return Encoding.UTF8.GetString(_jsonSerializer.ToBytes(payload).SerializedPayload);
        }
    }
}
