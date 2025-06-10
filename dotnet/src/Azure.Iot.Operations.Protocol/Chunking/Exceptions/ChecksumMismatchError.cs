// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Chunking.Exceptions;

/// <summary>
/// Exception thrown when the reassembled message checksum doesn't match the expected checksum.
/// </summary>
public class ChecksumMismatchError : ChunkingException
{
    /// <summary>
    /// Gets the expected checksum from the first chunk.
    /// </summary>
    public string ExpectedChecksum { get; }

    /// <summary>
    /// Gets the actual checksum calculated from the reassembled payload.
    /// </summary>
    public string ActualChecksum { get; }

    /// <summary>
    /// Gets the size of the reassembled payload.
    /// </summary>
    public long PayloadSize { get; }

    /// <summary>
    /// Gets the checksum algorithm that was used.
    /// </summary>
    public ChunkingChecksumAlgorithm ChecksumAlgorithm { get; }

    /// <summary>
    /// Gets the total number of chunks that were reassembled.
    /// </summary>
    public int TotalChunks { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChecksumMismatchError"/> class.
    /// </summary>
    /// <param name="messageId">The message ID with the checksum mismatch.</param>
    /// <param name="expectedChecksum">The expected checksum from the first chunk.</param>
    /// <param name="actualChecksum">The actual checksum calculated from the reassembled payload.</param>
    /// <param name="payloadSize">The size of the reassembled payload.</param>
    /// <param name="checksumAlgorithm">The checksum algorithm that was used.</param>
    /// <param name="totalChunks">The total number of chunks that were reassembled.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    public ChecksumMismatchError(
        string messageId,
        string expectedChecksum,
        string actualChecksum,
        long payloadSize,
        ChunkingChecksumAlgorithm checksumAlgorithm,
        int totalChunks,
        Exception? innerException = null)
        : base(messageId,
               $"Checksum verification failed. Expected: {expectedChecksum}, Actual: {actualChecksum}, Algorithm: {checksumAlgorithm}, Payload size: {payloadSize:N0} bytes, Chunks: {totalChunks}",
               null,
               innerException)
    {
        ExpectedChecksum = expectedChecksum ?? throw new ArgumentNullException(nameof(expectedChecksum));
        ActualChecksum = actualChecksum ?? throw new ArgumentNullException(nameof(actualChecksum));
        PayloadSize = payloadSize;
        ChecksumAlgorithm = checksumAlgorithm;
        TotalChunks = totalChunks;
    }

    /// <summary>
    /// Gets a value indicating whether the checksum mismatch might be due to data corruption.
    /// This is a heuristic based on the difference between expected and actual checksums.
    /// </summary>
    public bool PossibleDataCorruption => !string.Equals(ExpectedChecksum, ActualChecksum, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets diagnostic information about the checksum mismatch.
    /// </summary>
    /// <returns>A string containing diagnostic information.</returns>
    public string GetDiagnosticInfo()
    {
        return $"Checksum Mismatch Diagnostics:\n" +
               $"  Message ID: {MessageId}\n" +
               $"  Expected: {ExpectedChecksum}\n" +
               $"  Actual: {ActualChecksum}\n" +
               $"  Algorithm: {ChecksumAlgorithm}\n" +
               $"  Payload Size: {PayloadSize:N0} bytes\n" +
               $"  Total Chunks: {TotalChunks}\n" +
               $"  Possible Corruption: {PossibleDataCorruption}";
    }
}
