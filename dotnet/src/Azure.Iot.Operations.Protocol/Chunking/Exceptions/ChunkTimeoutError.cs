// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Azure.Iot.Operations.Protocol.Chunking.Exceptions;

/// <summary>
/// Exception thrown when a chunked message assembly times out before all chunks are received.
/// </summary>
public class ChunkTimeoutError : ChunkingException
{
    /// <summary>
    /// Gets the total number of chunks expected for the message.
    /// </summary>
    public int ExpectedChunks { get; }

    /// <summary>
    /// Gets the number of chunks that were actually received before the timeout.
    /// </summary>
    public int ReceivedChunks { get; }

    /// <summary>
    /// Gets the timeout duration that was exceeded.
    /// </summary>
    public TimeSpan TimeoutDuration { get; }

    /// <summary>
    /// Gets the time when the first chunk was received.
    /// </summary>
    public DateTime FirstChunkReceived { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkTimeoutError"/> class.
    /// </summary>
    /// <param name="messageId">The message ID that timed out.</param>
    /// <param name="expectedChunks">The total number of chunks expected.</param>
    /// <param name="receivedChunks">The number of chunks received before timeout.</param>
    /// <param name="timeoutDuration">The timeout duration that was exceeded.</param>
    /// <param name="firstChunkReceived">The time when the first chunk was received.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    public ChunkTimeoutError(
        string messageId,
        int expectedChunks,
        int receivedChunks,
        TimeSpan timeoutDuration,
        DateTime firstChunkReceived,
        Exception? innerException = null)
        : base(messageId, 
               $"Chunked message assembly timed out. Expected {expectedChunks} chunks, received {receivedChunks} chunks. Timeout: {timeoutDuration.TotalSeconds:F1}s",
               null,
               innerException)
    {
        ExpectedChunks = expectedChunks;
        ReceivedChunks = receivedChunks;
        TimeoutDuration = timeoutDuration;
        FirstChunkReceived = firstChunkReceived;
    }

    /// <summary>
    /// Gets the missing chunk indices that were not received before the timeout.
    /// </summary>
    /// <param name="receivedChunkIndices">The indices of chunks that were received.</param>
    /// <returns>An array of missing chunk indices.</returns>
    public int[] GetMissingChunkIndices(int[] receivedChunkIndices)
    {
        var missing = new List<int>();
        var receivedSet = new HashSet<int>(receivedChunkIndices);

        for (int i = 0; i < ExpectedChunks; i++)
        {
            if (!receivedSet.Contains(i))
            {
                missing.Add(i);
            }
        }

        return missing.ToArray();
    }
}
