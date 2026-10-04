// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.Chunking.Exceptions;
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
    private readonly uint? _remainingSeconds;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkedMessageSplitter"/> class.
    /// </summary>
    /// <param name="options">The chunking options.</param>
    public ChunkedMessageSplitter(ChunkingOptions options, uint? remainingSeconds = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _remainingSeconds = remainingSeconds;
    }

    /// <summary>
    /// Splits a message into chunks if the encoded packet would exceed the maximum packet size,
    /// otherwise returns the message unchanged.
    /// </summary>
    public static IReadOnlyList<MqttApplicationMessage> SplitIfNeeded(
        MqttApplicationMessage message,
        bool includeRemainingSeconds = false,
        ChunkingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ThrowIfReservedChunkProperty(message);

        int maxPacketSize = ChunkingConstants.PlaceholderMaxPacketSize;
        options ??= new ChunkingOptions();

        // Decided on the whole packet rather than the payload alone, since user properties are
        // unbounded and count toward the broker's limit just as much as the body does.
        return MqttPacketSizeCalculator.CalculatePublishSize(message) <= maxPacketSize
            ? [message]
            : new ChunkedMessageSplitter(
                options,
                includeRemainingSeconds ? uint.MaxValue : null).SplitMessage(message, maxPacketSize);
    }

    /// <summary>
    /// Splits a message into a header chunk, property chunks, and data chunks.
    /// </summary>
    /// <param name="message">The original message to split.</param>
    /// <param name="maxPacketSize">The maximum packet size allowed.</param>
    /// <returns>The chunked messages, starting with the header chunk.</returns>
    /// <remarks>
    /// Chunk 0 carries checksum metadata, property chunks carry the original user properties, and
    /// data chunks carry the payload. Properties needed to route and validate an individual packet
    /// are repeated on every chunk.
    /// </remarks>
    public IReadOnlyList<MqttApplicationMessage> SplitMessage(MqttApplicationMessage message, int maxPacketSize)
    {
        ArgumentNullException.ThrowIfNull(message);
        ThrowIfReservedChunkProperty(message);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPacketSize, 128); // minimum MQTT 5.0 protocol compliance.

        var payload = message.Payload;
        var messageId = Guid.NewGuid().ToString("D");
        var checksum = _options.Checksum.Compute(payload);

        var userProperties = new List<MqttUserProperty>(message.UserProperties ?? Enumerable.Empty<MqttUserProperty>());
        var perChunkUserProperties = userProperties
            .Where(p => ChunkingConstants.PerChunkUserProperties.Contains(p.Name, StringComparer.Ordinal))
            .ToList();

        List<List<MqttUserProperty>> propertyGroups = SplitUserProperties(
            message,
            userProperties,
            perChunkUserProperties,
            messageId,
            maxPacketSize);

        int maxChunkSize = payload.Length == 0
            ? 1
            : GetMaxDataChunkSize(message, perChunkUserProperties, messageId, maxPacketSize);
        long dataChunkCountLong = payload.Length == 0
            ? 0
            : ((payload.Length - 1) / maxChunkSize) + 1;
        long totalChunksLong = 1L + propertyGroups.Count + dataChunkCountLong;

        if (totalChunksLong > _options.MaxChunkCount)
        {
            throw new MessageTooLargeError(
                messageId,
                $"The message requires {totalChunksLong} chunks, which exceeds the limit of {_options.MaxChunkCount}.");
        }

        int dataChunkCount = checked((int)dataChunkCountLong);
        int totalChunks = checked((int)totalChunksLong);

        var chunks = new List<MqttApplicationMessage>(totalChunks)
        {
            CreateChunk(message, ReadOnlySequence<byte>.Empty, perChunkUserProperties, [], messageId, 0, totalChunks, ChunkKind.Head, _options.Checksum.Id, checksum),
        };

        long headSize = MqttPacketSizeCalculator.CalculatePublishSize(chunks[0]);
        if (headSize > maxPacketSize)
        {
            throw new MessageTooLargeError(
                messageId,
                $"The head chunk requires {headSize} bytes, which exceeds the maximum packet size of {maxPacketSize}.");
        }

        int chunkIndex = 1;
        foreach (List<MqttUserProperty> propertyGroup in propertyGroups)
        {
            chunks.Add(CreateChunk(message, ReadOnlySequence<byte>.Empty, perChunkUserProperties, propertyGroup, messageId, chunkIndex, totalChunks, ChunkKind.Property));
            chunkIndex++;
        }

        for (int dataIndex = 0; dataIndex < dataChunkCount; dataIndex++)
        {
            var chunkPayload = payload.Slice((long)dataIndex * maxChunkSize, Math.Min(maxChunkSize, payload.Length - ((long)dataIndex * maxChunkSize)));
            chunks.Add(CreateChunk(message, chunkPayload, perChunkUserProperties, [], messageId, chunkIndex, totalChunks, ChunkKind.Data));
            chunkIndex++;
        }

        Trace.TraceInformation($"Chunking: split a {payload.Length} byte payload for topic '{message.Topic}' into a header chunk, {propertyGroups.Count} property chunk(s), and {dataChunkCount} data chunk(s) of at most {maxChunkSize} bytes as message '{messageId}'.");

        return chunks;
    }

    private static void ThrowIfReservedChunkProperty(MqttApplicationMessage message)
    {
        if (message.UserProperties?.Any(p => p.Name == ChunkingConstants.ChunkUserProperty) == true)
        {
            throw ChunkAssemblyError.MalformedMetadata(
                Guid.Empty.ToString("D"),
                0,
                $"'{ChunkingConstants.ChunkUserProperty}' is reserved for protocol chunking.");
        }
    }

    private List<List<MqttUserProperty>> SplitUserProperties(
        MqttApplicationMessage message,
        List<MqttUserProperty> userProperties,
        List<MqttUserProperty> perChunkUserProperties,
        string messageId,
        int maxPacketSize)
    {
        List<List<MqttUserProperty>> groups = [];
        List<MqttUserProperty> current = [];

        foreach (MqttUserProperty property in userProperties)
        {
            current.Add(property);

            if (PropertyChunkFits(message, perChunkUserProperties, current, messageId, maxPacketSize))
            {
                continue;
            }

            current.RemoveAt(current.Count - 1);
            if (current.Count > 0)
            {
                groups.Add([.. current]);
                current.Clear();
            }

            current.Add(property);

            if (!PropertyChunkFits(message, perChunkUserProperties, current, messageId, maxPacketSize))
            {
                throw new MessageTooLargeError(
                    messageId,
                    $"User property '{property.Name}' cannot fit in one property chunk within the maximum packet size of {maxPacketSize}.");
            }
        }

        if (current.Count > 0)
        {
            groups.Add(current);
        }

        return groups;
    }

    private bool PropertyChunkFits(
        MqttApplicationMessage message,
        List<MqttUserProperty> perChunkUserProperties,
        List<MqttUserProperty> properties,
        string messageId,
        int maxPacketSize)
    {
        int probeTotalChunks = Math.Max(_options.MaxChunkCount, 2);
        MqttApplicationMessage probe = CreateChunk(
            message,
            ReadOnlySequence<byte>.Empty,
            perChunkUserProperties,
            properties,
            messageId,
            probeTotalChunks - 1,
            probeTotalChunks,
            ChunkKind.Property);

        return MqttPacketSizeCalculator.CalculatePublishSize(probe) <= maxPacketSize;
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
        int probeTotalChunks = Math.Max(_options.MaxChunkCount, 2);
        var probe = CreateChunk(message, ReadOnlySequence<byte>.Empty, perChunkUserProperties, [], messageId, probeTotalChunks - 1, probeTotalChunks, ChunkKind.Data);
        long emptySize = MqttPacketSizeCalculator.CalculatePublishSize(probe);

        if (emptySize >= maxPacketSize)
        {
            throw new MessageTooLargeError(
                messageId,
                $"A data chunk's own overhead is {emptySize} bytes, leaving no room for payload within the maximum packet size of {maxPacketSize}.");
        }

        int lowerBound = 0;
        int upperBound = maxPacketSize;
        while (lowerBound < upperBound)
        {
            int candidate = lowerBound + ((upperBound - lowerBound + 1) / 2);
            long encodedSize = MqttPacketSizeCalculator.CalculatePublishSize(probe, candidate);

            if (encodedSize <= maxPacketSize)
            {
                lowerBound = candidate;
            }
            else
            {
                upperBound = candidate - 1;
            }
        }

        if (lowerBound == 0)
        {
            throw new MessageTooLargeError(
                messageId,
                $"A data chunk cannot carry any payload within the maximum packet size of {maxPacketSize}.");
        }

        return lowerBound;
    }

    private MqttApplicationMessage CreateChunk(
        MqttApplicationMessage originalMessage,
        ReadOnlySequence<byte> chunkPayload,
        List<MqttUserProperty> perChunkUserProperties,
        List<MqttUserProperty> messageUserProperties,
        string messageId,
        int chunkIndex,
        int totalChunks,
        ChunkKind kind,
        string? checksumId = null,
        string? checksum = null)
    {
        ChunkMetadata metadata = kind switch
        {
            ChunkKind.Head => ChunkMetadata.CreateFirstChunk(messageId, totalChunks, checksumId!, checksum!),
            ChunkKind.Property => ChunkMetadata.CreatePropertyChunk(messageId, chunkIndex, totalChunks),
            _ => ChunkMetadata.CreateDataChunk(messageId, chunkIndex, totalChunks),
        };

        var chunkUserProperties = new List<MqttUserProperty>(perChunkUserProperties)
        {
            new(ChunkingConstants.ChunkUserProperty, metadata.Format(_remainingSeconds))
        };
        chunkUserProperties.AddRange(messageUserProperties);

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
