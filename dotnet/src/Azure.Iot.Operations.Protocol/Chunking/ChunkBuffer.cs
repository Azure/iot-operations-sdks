// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// The outcome of offering a chunk to a <see cref="ChunkBuffer"/>.
/// </summary>
internal sealed class ChunkBufferResult
{
    private static readonly MqttApplicationMessageReceivedEventArgs[] None = [];

    private ChunkBufferResult(
        MqttApplicationMessageReceivedEventArgs? reassembledMessage,
        IReadOnlyList<MqttApplicationMessageReceivedEventArgs> discardedChunks)
    {
        ReassembledMessage = reassembledMessage;
        DiscardedChunks = discardedChunks;
    }

    /// <summary>
    /// Gets the reassembled message, or null if the message is not yet complete or was discarded.
    /// </summary>
    /// <remarks>
    /// Acknowledging the reassembled message acknowledges every chunk it was built from.
    /// </remarks>
    public MqttApplicationMessageReceivedEventArgs? ReassembledMessage { get; }

    /// <summary>
    /// Gets the chunks the buffer has thrown away and no longer holds. They will never form a
    /// message, but the caller must still acknowledge them or they will stall the ack stream.
    /// </summary>
    public IReadOnlyList<MqttApplicationMessageReceivedEventArgs> DiscardedChunks { get; }

    /// <summary>
    /// The chunk was stored and more are expected. It must not be acknowledged yet.
    /// </summary>
    public static ChunkBufferResult Incomplete { get; } = new(null, None);

    /// <summary>
    /// A message was reassembled. Any <paramref name="discardedChunks"/> are unrelated chunks the
    /// buffer threw away while handling this one, typically from a message that expired.
    /// </summary>
    public static ChunkBufferResult Reassembled(
        MqttApplicationMessageReceivedEventArgs message,
        IReadOnlyList<MqttApplicationMessageReceivedEventArgs>? discardedChunks = null)
    {
        return new ChunkBufferResult(message, discardedChunks ?? None);
    }

    public static ChunkBufferResult Discard(IReadOnlyList<MqttApplicationMessageReceivedEventArgs> discardedChunks)
    {
        return new ChunkBufferResult(null, discardedChunks);
    }
}

/// <summary>
/// Reassembles chunked messages ahead of any protocol-level processing.
/// </summary>
/// <remarks>
/// This sits above the command response cache deliberately: every chunk of a request shares the
/// same correlation data, so letting individual chunks reach the cache would make each one look
/// like a duplicate of the first. See doc/dev/rpc-chunking-working-doc.md.
/// </remarks>
internal sealed class ChunkBuffer
{
    private readonly ChunkingOptions _options;
    private readonly Dictionary<string, Entry> _entries = [];
    private readonly object _lock = new();
    private long _bufferedBytes;

    public ChunkBuffer(ChunkingOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Determines whether a message carries chunk metadata.
    /// </summary>
    public static bool IsChunk(MqttApplicationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return message.UserProperties?.Any(p => p.Name == ChunkingConstants.ChunkUserProperty) == true;
    }

    /// <summary>
    /// Offers a chunk to the buffer.
    /// </summary>
    /// <param name="args">The received chunk.</param>
    /// <param name="now">The current time, used to expire stale partial messages.</param>
    /// <param name="expiresAt">The deadline by which the remaining chunks must arrive.</param>
    public ChunkBufferResult AddChunk(MqttApplicationMessageReceivedEventArgs args, DateTime now, DateTime expiresAt)
    {
        ArgumentNullException.ThrowIfNull(args);

        // Without an expiry there is no bound on how long a partial message could be retained.
        if (args.ApplicationMessage.MessageExpiryInterval == 0)
        {
            Trace.TraceWarning($"Discarding chunk published without a message expiry interval on topic '{args.ApplicationMessage.Topic}'.");
            return ChunkBufferResult.Discard([args]);
        }

        string? metadataValue = args.ApplicationMessage.UserProperties?
            .FirstOrDefault(p => p.Name == ChunkingConstants.ChunkUserProperty)?.Value;

        if (!ChunkMetadata.TryParse(metadataValue, out ChunkMetadata? metadata))
        {
            Trace.TraceWarning($"Discarding chunk with unparsable '{ChunkingConstants.ChunkUserProperty}' value '{metadataValue}'.");
            return ChunkBufferResult.Discard([args]);
        }

        lock (_lock)
        {
            List<MqttApplicationMessageReceivedEventArgs> abandoned = SweepExpired(now);

            ChunkBufferResult result = AddChunkUnderLock(args, metadata!, expiresAt);

            if (abandoned.Count == 0)
            {
                return result;
            }

            abandoned.AddRange(result.DiscardedChunks);
            return result.ReassembledMessage != null
                ? ChunkBufferResult.Reassembled(result.ReassembledMessage, abandoned)
                : ChunkBufferResult.Discard(abandoned);
        }
    }

    private ChunkBufferResult AddChunkUnderLock(
        MqttApplicationMessageReceivedEventArgs args,
        ChunkMetadata metadata,
        DateTime expiresAt)
    {
        if (!_entries.TryGetValue(metadata.MessageId, out Entry? entry))
        {
            entry = new Entry(new ChunkedMessageAssembler(0, _options.ChecksumAlgorithm), expiresAt);
            _entries[metadata.MessageId] = entry;
        }

        if (metadata.TotalChunks is int totalChunks)
        {
            if (totalChunks > _options.MaxChunkCount)
            {
                Trace.TraceWarning($"Discarding message '{metadata.MessageId}' split into {totalChunks} chunks, which exceeds the limit of {_options.MaxChunkCount}.");
                return ChunkBufferResult.Discard(Remove(metadata.MessageId, entry, args));
            }

            entry.Assembler.UpdateMetadata(totalChunks, metadata.Checksum, null);
        }

        long chunkSize = args.ApplicationMessage.Payload.Length;

        if (_options.ReassemblyBufferSizeLimit > 0 && _bufferedBytes + chunkSize > _options.ReassemblyBufferSizeLimit)
        {
            Trace.TraceWarning($"Discarding message '{metadata.MessageId}': buffering {chunkSize} more bytes would exceed the reassembly limit of {_options.ReassemblyBufferSizeLimit} bytes.");
            return ChunkBufferResult.Discard(Remove(metadata.MessageId, entry, args));
        }

        if (!entry.Assembler.AddChunk(metadata.ChunkIndex, args))
        {
            // A redelivery of a chunk we already hold. The retained copy will be acknowledged with the rest.
            return ChunkBufferResult.Discard([args]);
        }

        _bufferedBytes += chunkSize;

        if (!entry.Assembler.IsComplete)
        {
            Trace.TraceInformation($"Chunking: buffered chunk {metadata.ChunkIndex} of message '{metadata.MessageId}', {entry.Assembler.ReceivedChunkCount} chunk(s) held, {_bufferedBytes} byte(s) buffered in total.");
            return ChunkBufferResult.Incomplete;
        }

        int chunkCount = entry.Assembler.ReceivedChunkCount;

        _entries.Remove(metadata.MessageId);
        _bufferedBytes -= entry.Assembler.CurrentBufferSize;

        if (!entry.Assembler.TryReassemble(out MqttApplicationMessageReceivedEventArgs? reassembled))
        {
            Trace.TraceWarning($"Discarding message '{metadata.MessageId}': reassembly failed, most likely a checksum mismatch.");
            return ChunkBufferResult.Discard([.. entry.Assembler.ReceivedChunks]);
        }

        Trace.TraceInformation($"Chunking: reassembled message '{metadata.MessageId}' from {chunkCount} chunk(s) into {reassembled!.ApplicationMessage.Payload.Length} byte(s).");

        return ChunkBufferResult.Reassembled(reassembled);
    }

    private List<MqttApplicationMessageReceivedEventArgs> Remove(string messageId, Entry entry, MqttApplicationMessageReceivedEventArgs args)
    {
        _entries.Remove(messageId);
        _bufferedBytes -= entry.Assembler.CurrentBufferSize;

        List<MqttApplicationMessageReceivedEventArgs> discarded = [.. entry.Assembler.ReceivedChunks];
        discarded.Add(args);
        return discarded;
    }

    private List<MqttApplicationMessageReceivedEventArgs> SweepExpired(DateTime now)
    {
        List<MqttApplicationMessageReceivedEventArgs> abandoned = [];

        foreach (string messageId in _entries.Where(e => now >= e.Value.ExpiresAt).Select(e => e.Key).ToList())
        {
            Entry expired = _entries[messageId];
            _entries.Remove(messageId);
            _bufferedBytes -= expired.Assembler.CurrentBufferSize;
            abandoned.AddRange(expired.Assembler.ReceivedChunks);

            Trace.TraceWarning($"Abandoning partially received message '{messageId}': the remaining chunks did not arrive before it expired.");
        }

        return abandoned;
    }

    private sealed class Entry(ChunkedMessageAssembler assembler, DateTime expiresAt)
    {
        public ChunkedMessageAssembler Assembler { get; } = assembler;

        public DateTime ExpiresAt { get; } = expiresAt;
    }
}
