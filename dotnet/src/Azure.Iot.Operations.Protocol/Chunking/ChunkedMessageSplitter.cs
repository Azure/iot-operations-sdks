// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// Handles splitting large MQTT messages into smaller chunks.
/// </summary>
internal class ChunkedMessageSplitter
{
    private readonly ChunkingOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkedMessageSplitter"/> class.
    /// </summary>
    /// <param name="options">The chunking options.</param>
    public ChunkedMessageSplitter(ChunkingOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Splits a message into smaller chunks if necessary.
    /// </summary>
    /// <param name="message">The original message to split.</param>
    /// <param name="maxPacketSize">The maximum packet size allowed.</param>
    /// <returns>A list of chunked messages.</returns>
    public IReadOnlyList<MqttApplicationMessage> SplitMessage(MqttApplicationMessage message, int maxPacketSize)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPacketSize, 128); // minimum MQTT 5.0 protocol compliance.

        // Calculate the maximum size for each chunk's payload
        var maxChunkSize = GetMaxChunkSize(maxPacketSize);
        if (message.Payload.Length <= maxChunkSize)
        {
            throw new ArgumentException($"Message size {message.Payload.Length} is less than the maximum chunk size {maxChunkSize}.", nameof(message));
        }

        var payload = message.Payload;
        var totalChunks = (int)Math.Ceiling((double)payload.Length / maxChunkSize);

        // Generate a unique message ID
        var messageId = Guid.NewGuid().ToString("D");

        // Calculate checksum for the entire payload
        var checksum = ChecksumCalculator.CalculateChecksum(payload, _options.ChecksumAlgorithm);

        // Create a copy of the user properties
        var userProperties = new List<MqttUserProperty>(message.UserProperties ?? Enumerable.Empty<MqttUserProperty>());

        // Create chunks
        var chunks = new List<MqttApplicationMessage>(totalChunks);

        for (var chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
        {
            // Create chunk metadata
            var metadata = chunkIndex == 0
                ? ChunkMetadata.CreateFirstChunk(messageId, totalChunks, checksum, _options.ChunkTimeout)
                : ChunkMetadata.CreateSubsequentChunk(messageId, chunkIndex, _options.ChunkTimeout);

            // Serialize the metadata to JSON
            var metadataJson = JsonSerializer.Serialize(metadata);

            // Create user properties for this chunk
            var chunkUserProperties = new List<MqttUserProperty>(userProperties)
            {
                // Add the chunk metadata property
                new(ChunkingConstants.ChunkUserProperty, metadataJson)
            };

            // Extract the chunk payload
            var chunkStart = (long)chunkIndex * maxChunkSize;
            var chunkLength = Math.Min(maxChunkSize, payload.Length - chunkStart);
            var chunkPayload = payload.Slice(chunkStart, chunkLength);

            // Create a message for this chunk
            var chunkMessage = new MqttApplicationMessage(message.Topic, message.QualityOfServiceLevel)
            {
                Retain = message.Retain,
                Payload = chunkPayload,
                ContentType = message.ContentType,
                ResponseTopic = message.ResponseTopic,
                CorrelationData = message.CorrelationData,
                PayloadFormatIndicator = message.PayloadFormatIndicator,
                MessageExpiryInterval = message.MessageExpiryInterval,
                TopicAlias = message.TopicAlias,
                SubscriptionIdentifiers = message.SubscriptionIdentifiers,
                UserProperties = chunkUserProperties
            };

            chunks.Add(chunkMessage);
        }

        return chunks;
    }

    private int GetMaxChunkSize(int maxPacketSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxPacketSize, _options.StaticOverhead);
        // Subtract the static overhead to ensure we don't exceed the broker's limits
        return maxPacketSize - _options.StaticOverhead;
    }
}
