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
    /// Splits a message into chunks if the encoded packet would exceed the maximum packet size,
    /// otherwise returns the message unchanged.
    /// </summary>
    public static IReadOnlyList<MqttApplicationMessage> SplitIfNeeded(MqttApplicationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        int maxPacketSize = ChunkingConstants.PlaceholderMaxPacketSize;

        // Decided on the whole packet rather than the payload alone, since user properties are
        // unbounded and count toward the broker's limit just as much as the body does.
        return MqttPacketSizeCalculator.CalculatePublishSize(message) <= maxPacketSize
            ? [message]
            : new ChunkedMessageSplitter(new ChunkingOptions()).SplitMessage(message, maxPacketSize);
    }

    /// <summary>
    /// Splits a message into a header chunk followed by data chunks.
    /// </summary>
    /// <param name="message">The original message to split.</param>
    /// <param name="maxPacketSize">The maximum packet size allowed.</param>
    /// <returns>The chunked messages, starting with the header chunk.</returns>
    /// <remarks>
    /// Chunk 0 carries the full user property set and no payload; the payload is carried by chunks
    /// 1..n, which bear only the properties needed to deliver and reassemble them. Quarantining the
    /// unbounded, user-controlled properties in a single message means every data chunk has an
    /// entirely SDK-controlled property set, whose overhead can be measured rather than guessed.
    /// </remarks>
    public IReadOnlyList<MqttApplicationMessage> SplitMessage(MqttApplicationMessage message, int maxPacketSize)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPacketSize, 128); // minimum MQTT 5.0 protocol compliance.

        var payload = message.Payload;
        var messageId = Guid.NewGuid().ToString("D");
        var checksum = _options.Checksum.Compute(payload);

        var userProperties = new List<MqttUserProperty>(message.UserProperties ?? Enumerable.Empty<MqttUserProperty>());
        var perChunkUserProperties = userProperties
            .Where(p => ChunkingConstants.PerChunkUserProperties.Contains(p.Name, StringComparer.Ordinal))
            .ToList();

        var maxChunkSize = GetMaxDataChunkSize(message, perChunkUserProperties, messageId, maxPacketSize);
        var dataChunks = (int)Math.Ceiling((double)payload.Length / maxChunkSize);
        var totalChunks = dataChunks + 1;

        var chunks = new List<MqttApplicationMessage>(totalChunks)
        {
            CreateChunk(message, ReadOnlySequence<byte>.Empty, userProperties, messageId, 0, totalChunks, _options.Checksum.Id, checksum),
        };

        var headSize = MqttPacketSizeCalculator.CalculatePublishSize(chunks[0]);
        if (headSize > maxPacketSize)
        {
            throw new ArgumentException(
                $"The message's properties require {headSize} bytes, which exceeds the maximum packet size of {maxPacketSize}. Chunking cannot split properties across chunks.",
                nameof(message));
        }

        for (var chunkIndex = 1; chunkIndex < totalChunks; chunkIndex++)
        {
            var chunkPayload = ExtractChunkPayload(payload, chunkIndex, maxChunkSize);
            chunks.Add(CreateChunk(message, chunkPayload, perChunkUserProperties, messageId, chunkIndex, totalChunks, _options.Checksum.Id, checksum));
        }

        Trace.TraceInformation($"Chunking: split a {payload.Length} byte payload for topic '{message.Topic}' into a header chunk plus {dataChunks} data chunk(s) of at most {maxChunkSize} bytes as message '{messageId}'.");

        return chunks;
    }

    /// <summary>
    /// Determines how much payload a data chunk can carry, by sizing an empty one.
    /// </summary>
    /// <remarks>
    /// The widest chunk index the configured bound allows is used, because the index appears in the
    /// chunk metadata and a later index encodes to a slightly longer value; the real chunk count is
    /// not yet known at this point.
    /// </remarks>
    private int GetMaxDataChunkSize(
        MqttApplicationMessage message,
        List<MqttUserProperty> perChunkUserProperties,
        string messageId,
        int maxPacketSize)
    {
        var probe = CreateChunk(message, ReadOnlySequence<byte>.Empty, perChunkUserProperties, messageId, _options.MaxChunkCount, _options.MaxChunkCount, _options.Checksum.Id, string.Empty);
        var overhead = MqttPacketSizeCalculator.CalculatePublishSize(probe) + _options.StaticOverhead;

        if (overhead >= maxPacketSize)
        {
            throw new ArgumentException(
                $"A data chunk's own overhead is {overhead} bytes, leaving no room for payload within the maximum packet size of {maxPacketSize}.",
                nameof(message));
        }

        return (int)(maxPacketSize - overhead);
    }

    private static ReadOnlySequence<byte> ExtractChunkPayload(ReadOnlySequence<byte> payload, int chunkIndex, int maxChunkSize)
    {
        // Chunk 0 carries no payload, so data chunk n starts at offset (n - 1) * maxChunkSize.
        var chunkStart = (long)(chunkIndex - 1) * maxChunkSize;
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
        string checksumId,
        string checksum)
    {
        // Create chunk metadata
        var metadata = chunkIndex == 0
            ? ChunkMetadata.CreateFirstChunk(messageId, totalChunks, checksumId, checksum)
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
