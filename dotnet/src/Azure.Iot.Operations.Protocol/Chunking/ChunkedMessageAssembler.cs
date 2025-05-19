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

namespace Azure.Iot.Operations.Protocol.Chunking
{
    /// <summary>
    /// Handles the reassembly of chunked MQTT messages.
    /// </summary>
    internal class ChunkedMessageAssembler
    {
        private readonly Dictionary<int, MqttApplicationMessageReceivedEventArgs> _chunks = new();
        private readonly DateTime _creationTime = DateTime.UtcNow;
        private readonly object _lock = new();
        private int _totalChunks;
        private string? _checksum;
        private readonly ChunkingChecksumAlgorithm _checksumAlgorithm;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChunkedMessageAssembler"/> class.
        /// </summary>
        /// <param name="totalChunks">The total number of chunks expected (may be updated later).</param>
        /// <param name="checksumAlgorithm">The algorithm to use for checksum verification.</param>
        public ChunkedMessageAssembler(int totalChunks, ChunkingChecksumAlgorithm checksumAlgorithm)
        {
            _totalChunks = totalChunks;
            _checksumAlgorithm = checksumAlgorithm;
        }

        /// <summary>
        /// Gets a value indicating whether all chunks have been received.
        /// </summary>
        public bool IsComplete => _totalChunks > 0 && _chunks.Count == _totalChunks;

        /// <summary>
        /// Updates the metadata for this chunked message when the first chunk is received.
        /// </summary>
        /// <param name="totalChunks">The total number of chunks expected.</param>
        /// <param name="checksum">The checksum of the complete message.</param>
        public void UpdateMetadata(int totalChunks, string? checksum)
        {
            lock (_lock)
            {
                _totalChunks = totalChunks;
                _checksum = checksum;
            }
        }

        /// <summary>
        /// Adds a chunk to the assembler.
        /// </summary>
        /// <param name="chunkIndex">The index of the chunk.</param>
        /// <param name="args">The MQTT message received event args.</param>
        /// <returns>True if the chunk was added, false if it was already present.</returns>
        public bool AddChunk(int chunkIndex, MqttApplicationMessageReceivedEventArgs args)
        {
            lock (_lock)
            {
                if (_chunks.ContainsKey(chunkIndex))
                {
                    return false;
                }

                _chunks[chunkIndex] = args;
                return true;
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

                    // Calculate the total payload size
                    long totalSize = _chunks.Values.Sum(args => args.ApplicationMessage.Payload.Length);

                    // Create a memory stream with the exact capacity we need
                    using var memoryStream = new MemoryStream((int)totalSize);

                    // Write all chunks in order
                    for (int i = 0; i < _totalChunks; i++)
                    {
                        if (!_chunks.TryGetValue(i, out var chunkArgs))
                        {
                            // This should never happen if IsComplete is true
                            return false;
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
                        bool checksumValid = ChecksumCalculator.VerifyChecksum(reassembledPayload, _checksum, _checksumAlgorithm);
                        if (!checksumValid)
                        {
                            // Checksum verification failed
                            return false;
                        }
                    }

                    // Create a reassembled message without the chunking metadata
                    var userProperties = firstMessage.UserProperties?
                        .Where(p => p.Name != ChunkingConstants.ChunkUserProperty)
                        .ToList();

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
                        1, // TODO: Set the correct packet identifier
                        AcknowledgeHandler);

                    return true;
                }
                catch (Exception)
                {
                    // If reassembly fails for any reason, return false
                    return false;
                }
            }
        }

        private async Task AcknowledgeHandler(MqttApplicationMessageReceivedEventArgs reassembledArgs, CancellationToken ct)
        {
            // When acknowledging the reassembled message, acknowledge all the chunks
            var tasks = new List<Task>(_totalChunks);
            for (int i = 0; i < _totalChunks; i++)
            {
                if (_chunks.TryGetValue(i, out var chunk))
                {
                    tasks.Add(chunk.AcknowledgeAsync(ct));
                }
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if this assembler has expired based on the creation time.
        /// </summary>
        /// <param name="timeout">The timeout duration.</param>
        /// <returns>True if the assembler has expired, false otherwise.</returns>
        public bool HasExpired(TimeSpan timeout)
        {
            return DateTime.UtcNow - _creationTime > timeout;
        }
    }
}
