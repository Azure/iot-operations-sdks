// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

    public static ChunkBufferResult Discard(
        IReadOnlyList<MqttApplicationMessageReceivedEventArgs> discardedChunks)
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
internal sealed class ChunkBuffer : IAsyncDisposable
{
    private readonly ChunkingOptions _options;
    private readonly Dictionary<EntryKey, Entry> _entries = [];
    private readonly object _lock = new();
    private bool _disposed;

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
    public ChunkBufferResult AddChunk(
        MqttApplicationMessageReceivedEventArgs args,
        DateTime now,
        DateTime expiresAt,
        bool requireRemainingSeconds = false)
    {
        ArgumentNullException.ThrowIfNull(args);

        // Without an expiry there is no bound on how long a partial message could be retained.
        if (args.ApplicationMessage.MessageExpiryInterval == 0)
        {
            const string message = "Chunk was published without a positive message expiry interval.";
            Trace.TraceWarning($"Discarding {message} Topic: '{args.ApplicationMessage.Topic}'.");
            return ChunkBufferResult.Discard([args]);
        }

        string? metadataValue = args.ApplicationMessage.UserProperties?
            .FirstOrDefault(p => p.Name == ChunkingConstants.ChunkUserProperty)?.Value;

        if (!ChunkMetadata.TryParse(metadataValue, out ChunkMetadata? metadata))
        {
            string message = $"Chunk has an invalid '{ChunkingConstants.ChunkUserProperty}' value '{metadataValue}'.";
            Trace.TraceWarning($"Discarding chunk: {message}");
            return ChunkBufferResult.Discard([args]);
        }

        if (requireRemainingSeconds && !metadata!.RemainingSeconds.HasValue)
        {
            Trace.TraceWarning($"Discarding request chunk '{metadata.MessageId}': it does not carry its remaining operation budget.");
            return ChunkBufferResult.Discard([args]);
        }

        DateTime localDeadline = now + _options.MaxReassemblyWindow;
        if (localDeadline < expiresAt)
        {
            expiresAt = localDeadline;
        }

        if (requireRemainingSeconds)
        {
            double declaredSeconds = Math.Min(
                metadata!.RemainingSeconds!.Value,
                _options.MaxReassemblyWindow.TotalSeconds);
            DateTime declaredDeadline = now.AddSeconds(declaredSeconds);
            if (declaredDeadline < expiresAt)
            {
                expiresAt = declaredDeadline;
            }
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return ChunkBufferResult.Discard([args]);
            }

            if (expiresAt <= now)
            {
                Trace.TraceWarning($"Discarding chunked message '{metadata!.MessageId}': it expired before it could be buffered.");
                return ChunkBufferResult.Discard([args]);
            }

            return AddChunkUnderLock(args, metadata!, now, expiresAt);
        }
    }

    private ChunkBufferResult AddChunkUnderLock(
        MqttApplicationMessageReceivedEventArgs args,
        ChunkMetadata metadata,
        DateTime now,
        DateTime expiresAt)
    {
        int totalChunks = metadata.TotalChunks;
        EntryKey key = EntryKey.Create(metadata.MessageId, args.ApplicationMessage);
        _entries.TryGetValue(key, out Entry? entry);

        if (totalChunks > _options.MaxChunkCount)
        {
            Trace.TraceWarning($"Discarding message '{metadata.MessageId}': it declares {totalChunks} chunks, which exceeds the limit of {_options.MaxChunkCount}.");
            return Discard(key, entry, args);
        }

        if (entry == null)
        {
            entry = new Entry(new ChunkedMessageAssembler(totalChunks));
            _entries[key] = entry;
            _ = ExpireAsync(key, entry, expiresAt - now);
        }
        else if (entry.Assembler.TotalChunks != totalChunks)
        {
            Trace.TraceWarning($"Discarding message '{metadata.MessageId}': chunks disagree about the total chunk count.");
            return Discard(key, entry, args);
        }

        if (metadata.Kind == ChunkKind.Head)
        {
            // Verifying with anything other than the algorithm the sender used would report a
            // mismatch indistinguishable from corruption, so an unknown one is refused outright.
            IChunkChecksum? checksum = _options.ResolveChecksum(metadata.ChecksumId!);
            if (checksum == null)
            {
                Trace.TraceWarning($"Discarding message '{metadata.MessageId}': checksum algorithm '{metadata.ChecksumId}' is not supported.");
                return Discard(key, entry, args);
            }

            if (!entry.Assembler.TryUpdateMetadata(totalChunks, metadata.ChecksumId!, metadata.Checksum!, checksum))
            {
                Trace.TraceWarning($"Discarding message '{metadata.MessageId}': head chunks disagree about checksum metadata.");
                return Discard(key, entry, args);
            }
        }

        if (!entry.Assembler.IsChunkRoleConsistent(metadata))
        {
            Trace.TraceWarning($"Discarding message '{metadata.MessageId}': chunk roles conflict or property and data indices are interleaved.");
            return Discard(key, entry, args);
        }

        bool isNewIndex = entry.Assembler.AddChunk(metadata, args, out MqttApplicationMessageReceivedEventArgs? previous);

        if (!isNewIndex)
        {
            Trace.TraceInformation($"Chunking: chunk {metadata.ChunkIndex} of message '{metadata.MessageId}' was redelivered; holding the new delivery in place of the old one.");
            return ReferenceEquals(previous, args)
                ? ChunkBufferResult.Incomplete
                : ChunkBufferResult.Discard([previous!]);
        }

        if (!entry.Assembler.IsComplete)
        {
            Trace.TraceInformation($"Chunking: buffered chunk {metadata.ChunkIndex} of message '{metadata.MessageId}', {entry.Assembler.ReceivedChunkCount} chunk(s) held.");
            return ChunkBufferResult.Incomplete;
        }

        int chunkCount = entry.Assembler.ReceivedChunkCount;

        _entries.Remove(key);
        entry.CancelExpiration();

        if (!entry.Assembler.TryReassemble(out MqttApplicationMessageReceivedEventArgs? reassembled))
        {
            Trace.TraceWarning($"Discarding message '{metadata.MessageId}': it could not be reassembled or its checksum did not match.");
            return ChunkBufferResult.Discard([.. entry.Assembler.ReceivedChunks]);
        }

        Trace.TraceInformation($"Chunking: reassembled message '{metadata.MessageId}' from {chunkCount} chunk(s) into {reassembled!.ApplicationMessage.Payload.Length} byte(s).");

        return ChunkBufferResult.Reassembled(reassembled);
    }

    private ChunkBufferResult Discard(EntryKey key, Entry? entry, MqttApplicationMessageReceivedEventArgs args)
    {
        if (entry == null)
        {
            return ChunkBufferResult.Discard([args]);
        }

        _entries.Remove(key);
        entry.CancelExpiration();

        List<MqttApplicationMessageReceivedEventArgs> discarded = [.. entry.Assembler.ReceivedChunks];
        discarded.Add(args);
        return ChunkBufferResult.Discard(discarded);
    }

    private async Task ExpireAsync(EntryKey key, Entry entry, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, entry.ExpirationCancellation.Token).ConfigureAwait(false);

            List<MqttApplicationMessageReceivedEventArgs> abandoned = [];
            lock (_lock)
            {
                if (!_disposed
                    && _entries.TryGetValue(key, out Entry? current)
                    && ReferenceEquals(current, entry))
                {
                    _entries.Remove(key);
                    abandoned = [.. entry.Assembler.ReceivedChunks];
                }
            }

            if (abandoned.Count > 0)
            {
                // Held deliveries must be released even when the message never completes, or the
                // in-order ack stream stalls behind them.
                Trace.TraceWarning($"Abandoning partially received message '{key.MessageId}' with {abandoned.Count} chunk(s) held.");
                await AcknowledgeDiscardedAsync(ChunkBufferResult.Discard(abandoned)).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Completion, rejection, or disposal cancelled this entry's timer.
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Failed to expire chunked message '{key.MessageId}': {ex}");
        }
        finally
        {
            entry.ExpirationCancellation.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        List<Entry> entries;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            entries = [.. _entries.Values];
            _entries.Clear();
            foreach (Entry entry in entries)
            {
                entry.CancelExpiration();
            }
        }

        await AcknowledgeDiscardedAsync(ChunkBufferResult.Discard(
            entries.SelectMany(entry => entry.Assembler.ReceivedChunks).ToList())).ConfigureAwait(false);
    }

    internal static async Task AcknowledgeDiscardedAsync(ChunkBufferResult result)
    {
        foreach (MqttApplicationMessageReceivedEventArgs discarded in result.DiscardedChunks)
        {
            try
            {
                await discarded.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed to release a discarded chunk delivery: {ex}");
            }
        }
    }

    private sealed class Entry(ChunkedMessageAssembler assembler)
    {
        public ChunkedMessageAssembler Assembler { get; } = assembler;

        public CancellationTokenSource ExpirationCancellation { get; } = new();

        public void CancelExpiration()
        {
            try
            {
                ExpirationCancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The timer already finished and released its cancellation source.
            }
        }
    }

    private readonly record struct EntryKey(
        string MessageId,
        string Topic,
        string? ResponseTopic,
        string CorrelationData)
    {
        public static EntryKey Create(string messageId, MqttApplicationMessage message) => new(
            messageId,
            message.Topic,
            message.ResponseTopic,
            Convert.ToBase64String(message.CorrelationData ?? []));
    }
}
