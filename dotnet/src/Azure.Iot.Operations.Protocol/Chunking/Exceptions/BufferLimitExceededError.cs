// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Chunking.Exceptions;

/// <summary>
/// Exception thrown when the reassembly buffer size limit is exceeded.
/// </summary>
public class BufferLimitExceededError : ChunkingException
{
    /// <summary>
    /// Gets the current total buffer size across all active message assemblers.
    /// </summary>
    public long CurrentBufferSize { get; }

    /// <summary>
    /// Gets the configured buffer size limit that was exceeded.
    /// </summary>
    public long BufferLimit { get; }

    /// <summary>
    /// Gets the size of the chunk that would have exceeded the limit.
    /// </summary>
    public long ChunkSize { get; }

    /// <summary>
    /// Gets the number of active message assemblers when the limit was exceeded.
    /// </summary>
    public int ActiveAssemblers { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferLimitExceededError"/> class.
    /// </summary>
    /// <param name="messageId">The message ID of the chunk that would exceed the limit.</param>
    /// <param name="chunkIndex">The index of the chunk that would exceed the limit.</param>
    /// <param name="currentBufferSize">The current total buffer size.</param>
    /// <param name="bufferLimit">The configured buffer size limit.</param>
    /// <param name="chunkSize">The size of the chunk that would exceed the limit.</param>
    /// <param name="activeAssemblers">The number of active message assemblers.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    public BufferLimitExceededError(
        string messageId,
        int chunkIndex,
        long currentBufferSize,
        long bufferLimit,
        long chunkSize,
        int activeAssemblers,
        Exception? innerException = null)
        : base(messageId,
               $"Reassembly buffer limit exceeded. Current: {currentBufferSize:N0} bytes, Limit: {bufferLimit:N0} bytes, Chunk size: {chunkSize:N0} bytes, Active assemblers: {activeAssemblers}",
               chunkIndex,
               innerException)
    {
        CurrentBufferSize = currentBufferSize;
        BufferLimit = bufferLimit;
        ChunkSize = chunkSize;
        ActiveAssemblers = activeAssemblers;
    }

    /// <summary>
    /// Gets the amount by which the buffer limit would be exceeded.
    /// </summary>
    public long ExcessBytes => CurrentBufferSize + ChunkSize - BufferLimit;

    /// <summary>
    /// Gets the current buffer utilization as a percentage.
    /// </summary>
    public double BufferUtilizationPercent => (double)CurrentBufferSize / BufferLimit * 100.0;

    /// <summary>
    /// Gets the buffer utilization percentage if the chunk were accepted.
    /// </summary>
    public double ProjectedUtilizationPercent => (double)(CurrentBufferSize + ChunkSize) / BufferLimit * 100.0;
}
