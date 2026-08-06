// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
    /// Splits a message into chunks if its payload exceeds the maximum chunk size, otherwise
    /// returns the message unchanged.
    /// </summary>
    public static IReadOnlyList<MqttApplicationMessage> SplitIfNeeded(MqttApplicationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        ChunkingOptions options = new();
        int maxChunkSize = Utils.GetMaxChunkSize(ChunkingConstants.PlaceholderMaxPacketSize, options.StaticOverhead);

        return message.Payload.Length <= maxChunkSize
            ? [message]
            : new ChunkedMessageSplitter(options).SplitMessage(message, ChunkingConstants.PlaceholderMaxPacketSize);
    }

    /// <summary>
    /// Splits a message into smaller chunks if necessary.
    /// </summary>
    /// <param name="message">The original message to split.</param>
    /// <param name="maxPacketSize">The maximum packet size allowed.</param>
    /// <returns>A list of chunked messages.</returns>
    public IReadOnlyList<MqttApplicationMessage> SplitMessage(MqttApplicationMessage message, int maxPacketSize)
    {
        var maxChunkSize = ValidateAndGetMaxChunkSize(message, maxPacketSize);
        var (payload, totalChunks, messageId, checksum, userProperties) = PrepareChunkingMetadata(message, maxChunkSize);

        var perChunkUserProperties = userProperties
            .Where(p => ChunkingConstants.PerChunkUserProperties.Contains(p.Name, StringComparer.Ordinal))
            .ToList();

        // Create chunks
        var chunks = new List<MqttApplicationMessage>(totalChunks);

        for (var chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
        {
            var chunkPayload = ChunkedMessageSplitter.ExtractChunkPayload(payload, chunkIndex, maxChunkSize);
            var chunkMessage = ChunkedMessageSplitter.CreateChunk(message, chunkPayload, chunkIndex == 0 ? userProperties : perChunkUserProperties, messageId, chunkIndex, totalChunks, checksum);
            chunks.Add(chunkMessage);
        }

        Trace.TraceInformation($"Chunking: split a {payload.Length} byte payload for topic '{message.Topic}' into {totalChunks} chunk(s) of at most {maxChunkSize} bytes as message '{messageId}'.");

        return chunks;
    }

    private int ValidateAndGetMaxChunkSize(MqttApplicationMessage message, int maxPacketSize)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPacketSize, 128); // minimum MQTT 5.0 protocol compliance.

        // Calculate the maximum size for each chunk's payload
        var maxChunkSize = Utils.GetMaxChunkSize(maxPacketSize, _options.StaticOverhead);
        if (message.Payload.Length <= maxChunkSize)
        {
            throw new ArgumentException($"Message size {message.Payload.Length} is less than the maximum chunk size {maxChunkSize}.", nameof(message));
        }

        return maxChunkSize;
    }

    private (ReadOnlySequence<byte> Payload, int TotalChunks, string MessageId, string Checksum, List<MqttUserProperty> UserProperties)
        PrepareChunkingMetadata(MqttApplicationMessage message, int maxChunkSize)
    {
        var payload = message.Payload;
        var totalChunks = (int)Math.Ceiling((double)payload.Length / maxChunkSize);

        // Generate a unique message ID
        var messageId = Guid.NewGuid().ToString("D");

        // Calculate checksum for the entire payload
        var checksum = ChecksumCalculator.CalculateChecksum(payload, _options.ChecksumAlgorithm);

        // Create a copy of the user properties
        var userProperties = new List<MqttUserProperty>(message.UserProperties ?? Enumerable.Empty<MqttUserProperty>());

        return (payload, totalChunks, messageId, checksum, userProperties);
    }

    private static ReadOnlySequence<byte> ExtractChunkPayload(ReadOnlySequence<byte> payload, int chunkIndex, int maxChunkSize)
    {
        var chunkStart = (long)chunkIndex * maxChunkSize;
        var chunkLength = Math.Min(maxChunkSize, payload.Length - chunkStart);
        return payload.Slice(chunkStart, chunkLength);
    }

    private static MqttApplicationMessage CreateChunk(
        MqttApplicationMessage originalMessage,
        ReadOnlySequence<byte> chunkPayload,
        List<MqttUserProperty> userProperties,
        string messageId,
        int chunkIndex,
        int totalChunks,
        string checksum)
    {
        // Create chunk metadata
        var metadata = chunkIndex == 0
            ? ChunkMetadata.CreateFirstChunk(messageId, totalChunks, checksum)
            : ChunkMetadata.CreateSubsequentChunk(messageId, chunkIndex);

        // Create user properties for this chunk
        var chunkUserProperties = new List<MqttUserProperty>(userProperties)
        {
            // Add the chunk metadata property
            new(ChunkingConstants.ChunkUserProperty, metadata.Format())
        };

        // Create a message for this chunk
        return new MqttApplicationMessage(originalMessage.Topic, originalMessage.QualityOfServiceLevel)
        {
            Retain = originalMessage.Retain,
            Payload = chunkPayload,
            ContentType = originalMessage.ContentType,
            ResponseTopic = originalMessage.ResponseTopic,
            CorrelationData = originalMessage.CorrelationData,
            PayloadFormatIndicator = originalMessage.PayloadFormatIndicator,
            MessageExpiryInterval = originalMessage.MessageExpiryInterval,
            TopicAlias = originalMessage.TopicAlias,
            SubscriptionIdentifiers = originalMessage.SubscriptionIdentifiers,
            UserProperties = chunkUserProperties
        };
    }
}
