﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol
{
    public interface IPayloadSerializer
    {
        /// <summary>
        /// Serialize the provided object.
        /// </summary>
        /// <typeparam name="T">The type to serialize</typeparam>
        /// <param name="payload">The object to serialize</param>
        /// <returns>The serialized payload in a byte[] and the content type + payload format indicator used when serializing.</returns>
        public SerializedPayloadContext ToBytes<T>(T? payload) where T : class;

        /// <summary>
        /// Deserialize the provided payload.
        /// </summary>
        /// <typeparam name="T">The type to deserialize into.</typeparam>
        /// <param name="payload">The byte array to deserialize.</param>
        /// <param name="contentType">The content type of the MQTT message received with this payload.</param>
        /// <param name="payloadFormatIndicator">The payload format indicator of the MQTT message received with this payload.</param>
        /// <returns>The deserialized object.</returns>
        public T FromBytes<T>(byte[]? payload, string? contentType = null, int? payloadFormatIndicator = null) where T : class;
    }
}
