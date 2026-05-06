// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;
using System.Text.Json;
using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol.Models;

namespace ManagementActionConnector.Handlers
{
    /// <summary>Small helpers shared by the three sample handlers.</summary>
    internal static class ResponseHelpers
    {
        /// <summary>Serialize <paramref name="value"/> as JSON and wrap it in a successful response.</summary>
        public static ManagementActionResponse Json<T>(T value)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            return new ManagementActionResponse
            {
                Payload = new ReadOnlySequence<byte>(bytes),
                ContentType = "application/json",
                CloudEvent = null,
                FormatIndicator = MqttPayloadFormatIndicator.CharacterData,
            };
        }

        /// <summary>Build an application-level error response.</summary>
        public static ManagementActionResponse ApplicationError(string errorCode, string errorPayload)
            => new()
            {
                Payload = ReadOnlySequence<byte>.Empty,
                ContentType = "application/json",
                CloudEvent = null,
                ApplicationError = new ManagementActionApplicationError
                {
                    ErrorCode = errorCode,
                    ErrorPayload = errorPayload,
                },
            };
    }
}
