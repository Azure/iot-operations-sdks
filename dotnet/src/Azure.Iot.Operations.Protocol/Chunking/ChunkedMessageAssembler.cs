// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Events;
using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// Handles the reassembly of chunked MQTT messages.
/// </summary>
internal class ChunkedMessageAssembler
{
    private readonly Dictionary<int, MqttApplicationMessageReceivedEventArgs> _chunks = new();
    private readonly Dictionary<int, ChunkKind> _chunkKinds = new();
    private readonly object _lock = new();
    private string? _checksumId;
    private string? _checksum;
    private IChunkChecksum? _checksumAlgorithm;

    /// <summary>
    /// Gets the number of chunks received so far.
    /// </summary>
    public int ReceivedChunkCount
    {
        get
        {
            lock (_lock)
            {
                return _chunks.Count;
            }
        }
    }

    /// <summary>
    /// Gets the total number of chunks expected.
    /// </summary>
    public int TotalChunks { get; private set; }

    /// <summary>
    /// Gets the chunks received so far, for callers that must acknowledge them when a message is
    /// abandoned instead of reassembled.
    /// </summary>
    public IReadOnlyCollection<MqttApplicationMessageReceivedEventArgs> ReceivedChunks
    {
        get
        {
            lock (_lock)
            {
                return _chunks.Values.ToList();
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkedMessageAssembler"/> class.
    /// </summary>
    /// <param name="totalChunks">The total number of chunks expected (may be updated later).</param>
    public ChunkedMessageAssembler(int totalChunks)
    {
        TotalChunks = totalChunks;
    }

    /// <summary>
    /// Gets a value indicating whether all chunks have been received.
    /// </summary>
    public bool IsComplete => TotalChunks > 0 && _chunks.Count == TotalChunks;

    /// <summary>
    /// Records checksum metadata from the head chunk, or verifies that a redelivered head agrees
    /// with the metadata already recorded.
    /// </summary>
    public bool TryUpdateMetadata(
        int totalChunks,
        string checksumId,
        string checksum,
        IChunkChecksum checksumAlgorithm)
    {
        lock (_lock)
        {
            if (_checksumId != null || _checksum != null)
            {
                return TotalChunks == totalChunks
                    && string.Equals(_checksumId, checksumId, StringComparison.Ordinal)
                    && string.Equals(_checksum, checksum, StringComparison.Ordinal);
            }

            TotalChunks = totalChunks;
            _checksumId = checksumId;
            _checksum = checksum;
            _checksumAlgorithm = checksumAlgorithm;
            return true;
        }
    }

    /// <summary>
    /// Adds a parsed chunk to the assembler, or replaces the delivery already held at that index.
    /// </summary>
    public bool AddChunk(
        ChunkMetadata metadata,
        MqttApplicationMessageReceivedEventArgs args,
        out MqttApplicationMessageReceivedEventArgs? previous)
    {
        lock (_lock)
        {
            _chunks.TryGetValue(metadata.ChunkIndex, out previous);
            _chunkKinds[metadata.ChunkIndex] = metadata.Kind;
            return AddChunkUnderLock(metadata.ChunkIndex, args);
        }
    }

    /// <summary>
    /// Checks that this chunk's role agrees with any chunk already held at the same index and that
    /// property-chunk indices remain before data-chunk indices.
    /// </summary>
    public bool IsChunkRoleConsistent(ChunkMetadata metadata)
    {
        lock (_lock)
        {
            if (_chunkKinds.TryGetValue(metadata.ChunkIndex, out ChunkKind existingKind)
                && existingKind != metadata.Kind)
            {
                return false;
            }

            return metadata.Kind switch
            {
                ChunkKind.Property => !_chunkKinds.Any(chunk =>
                    chunk.Value == ChunkKind.Data && chunk.Key < metadata.ChunkIndex),
                ChunkKind.Data => !_chunkKinds.Any(chunk =>
                    chunk.Value == ChunkKind.Property && chunk.Key > metadata.ChunkIndex),
                _ => true,
            };
        }
    }

    /// <summary>
    /// Attempts to reassemble the complete message from all chunks.
    /// </summary>
    /// <param name="reassembledArgs">The reassembled message event args.</param>
    /// <returns>True if reassembly was successful, false otherwise.</returns>
    public bool TryReassemble(out MqttApplicationMessageReceivedEventArgs? reassembledArgs)
    {
        reassembledArgs = null;

        lock (_lock)
        {
            if (!IsComplete)
            {
                return false;
            }

            try
            {
                // Get the first chunk to use as a template for the reassembled message
                var firstChunk = _chunks[0];
                var firstMessage = firstChunk.ApplicationMessage;

                if (!HasValidChunkRoleSequence())
                {
                    return false;
                }

                long totalSize = _chunks
                    .Where(chunk => _chunkKinds[chunk.Key] == ChunkKind.Data)
                    .Sum(chunk => chunk.Value.ApplicationMessage.Payload.Length);

                if (totalSize > Array.MaxLength)
                {
                    return false;
                }

                // Create a memory stream with the exact capacity we need
                using var memoryStream = new MemoryStream((int)totalSize);

                // Write all chunks in order
                for (int i = 0; i < TotalChunks; i++)
                {
                    if (!_chunks.TryGetValue(i, out var chunkArgs))
                    {
                        // This should never happen if IsComplete is true
                        return false;
                    }

                    if (_chunkKinds[i] != ChunkKind.Data)
                    {
                        continue;
                    }

                    var payload = chunkArgs.ApplicationMessage.Payload;
                    foreach (ReadOnlyMemory<byte> memory in payload)
                    {
                        memoryStream.Write(memory.Span);
                    }
                }

                // Convert to ReadOnlySequence for checksum verification
                memoryStream.Position = 0;
                ReadOnlySequence<byte> reassembledPayload = new ReadOnlySequence<byte>(memoryStream.ToArray());

                // Verify the checksum if provided
                if (!string.IsNullOrEmpty(_checksum))
                {
                    if (_checksumAlgorithm == null)
                    {
                        return false;
                    }

                    string actual = _checksumAlgorithm.Compute(reassembledPayload);
                    if (!string.Equals(actual, _checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        // Checksum verification failed
                        return false;
                    }
                }

                List<MqttUserProperty>? userProperties = ReassembleUserProperties();
                if (userProperties == null)
                {
                    return false;
                }

                var reassembledMessage = new MqttApplicationMessage(firstMessage.Topic, firstMessage.QualityOfServiceLevel)
                {
                    Retain = firstMessage.Retain,
                    Payload = reassembledPayload,
                    ContentType = firstMessage.ContentType,
                    ResponseTopic = firstMessage.ResponseTopic,
                    CorrelationData = firstMessage.CorrelationData,
                    PayloadFormatIndicator = firstMessage.PayloadFormatIndicator,
                    MessageExpiryInterval = firstMessage.MessageExpiryInterval,
                    TopicAlias = firstMessage.TopicAlias,
                    SubscriptionIdentifiers = firstMessage.SubscriptionIdentifiers,
                    UserProperties = userProperties
                };

                // Create event args for the reassembled message
                reassembledArgs = new MqttApplicationMessageReceivedEventArgs(
                    firstChunk.ClientId,
                    reassembledMessage,
                    firstChunk.PacketIdentifier,
                    AcknowledgeHandler);

                return true;
            }
            catch (OutOfMemoryException)
            {
                throw;
            }
            catch (Exception)
            {
                // If reassembly fails for any reason, return false
                return false;
            }
        }
    }

    private bool AddChunkUnderLock(int chunkIndex, MqttApplicationMessageReceivedEventArgs args)
    {
        bool isNewIndex = !_chunks.ContainsKey(chunkIndex);
        _chunks[chunkIndex] = args;
        return isNewIndex;
    }

    private bool HasValidChunkRoleSequence()
    {
        if (!_chunkKinds.TryGetValue(0, out ChunkKind firstKind) || firstKind != ChunkKind.Head)
        {
            return false;
        }

        bool sawData = false;
        for (int i = 0; i < TotalChunks; i++)
        {
            if (!_chunkKinds.TryGetValue(i, out ChunkKind kind))
            {
                return false;
            }

            if (kind == ChunkKind.Head)
            {
                if (i != 0 || _chunks[i].ApplicationMessage.Payload.Length != 0)
                {
                    return false;
                }
            }
            else if (kind == ChunkKind.Property)
            {
                if (sawData || _chunks[i].ApplicationMessage.Payload.Length != 0)
                {
                    return false;
                }
            }
            else
            {
                sawData = true;
            }
        }

        return true;
    }

    private List<MqttUserProperty>? ReassembleUserProperties()
    {
        List<MqttUserProperty> userProperties = [];

        for (int i = 0; i < TotalChunks; i++)
        {
            List<MqttUserProperty> chunkProperties = _chunks[i].ApplicationMessage.UserProperties ?? [];
            int metadataIndex = chunkProperties.FindIndex(p => p.Name == ChunkingConstants.ChunkUserProperty);
            if (metadataIndex < 0
                || chunkProperties.Count(p => p.Name == ChunkingConstants.ChunkUserProperty) != 1)
            {
                return null;
            }

            if (_chunkKinds[i] == ChunkKind.Property)
            {
                if (metadataIndex == chunkProperties.Count - 1)
                {
                    return null;
                }

                userProperties.AddRange(chunkProperties.Skip(metadataIndex + 1));
            }
            else if (metadataIndex != chunkProperties.Count - 1)
            {
                return null;
            }
        }

        return userProperties;
    }

    private async Task AcknowledgeHandler(MqttApplicationMessageReceivedEventArgs reassembledArgs, CancellationToken ct)
    {
        // When acknowledging the reassembled message, acknowledge all the chunks
        var tasks = new List<Task>(TotalChunks);
        for (int i = 0; i < TotalChunks; i++)
        {
            if (_chunks.TryGetValue(i, out var chunk))
            {
                tasks.Add(chunk.AcknowledgeAsync(ct));
            }
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

}
