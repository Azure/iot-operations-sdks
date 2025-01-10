// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol
{
    public interface IPayloadSerializer
    {
        /// <summary>
        /// The content type that this serializer will use when serializing and deserializing unless a different content type is specified.
        /// </summary>
        string DefaultContentType { get; }

        /// <summary>
        /// The payload format indicator that this serializer will use when serializing and deserializing unless a different payload format indicator is specified.
        /// </summary>
        int DefaultPayloadFormatIndicator { get; }

        /// <summary>
        /// Serialize the provided object.
        /// </summary>
        /// <typeparam name="T">The type to serialize</typeparam>
        /// <param name="payload">The object to serialize</param>
        /// <param name="contentType">An optional override of the default content type specified in <see cref="DefaultContentType"/>.</param>
        /// <param name="payloadFormatIndicator">An optional override of the default payload format indicator specified in <see cref="DefaultContentType"/>.</param>
        /// <returns>The serialized payload in a byte[] and the content type + payload format indicator used when serializing.</returns>
        SerializedPayloadContext ToBytes<T>(T? payload, string? contentType, int? payloadFormatIndicator) where T : class;

        /// <summary>
        /// Deserialize the provided payload.
        /// </summary>
        /// <typeparam name="T">The type to deserialize into.</typeparam>
        /// <param name="payload">The byte array to deserialize.</param>
        /// <param name="contentType">An optional override of the default content type specified in <see cref="DefaultContentType"/>.</param>
        /// <param name="payloadFormatIndicator">An optional override of the default payload format indicator specified in <see cref="DefaultContentType"/>.</param>
        /// <returns>The deserialized object and the content type + payload format indicator used when serializing.</returns>
        DeserializedPayloadContext<T> FromBytes<T>(byte[]? payload, string? contentType, int? payloadFormatIndicator) where T : class;
    }
}
