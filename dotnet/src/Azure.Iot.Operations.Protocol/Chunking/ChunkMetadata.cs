// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// Represents the metadata for a chunk of a larger MQTT message.
/// </summary>
/// <remarks>
/// Serialized into the <see cref="ChunkingConstants.ChunkUserProperty"/> user property as a
/// colon-separated string, per ADR 0023:
/// <c>messageId:chunkIndex:totalChunks:checksum</c> for the first chunk, and
/// <c>messageId:chunkIndex</c> for every subsequent chunk.
/// </remarks>
internal sealed class ChunkMetadata
{
    private ChunkMetadata(string messageId, int chunkIndex, int? totalChunks, string? checksum)
    {
        MessageId = messageId;
        ChunkIndex = chunkIndex;
        TotalChunks = totalChunks;
        Checksum = checksum;
    }

    /// <summary>
    /// Gets the unique identifier for the chunked message.
    /// </summary>
    public string MessageId { get; }

    /// <summary>
    /// Gets the index of this chunk in the sequence.
    /// </summary>
    public int ChunkIndex { get; }

    /// <summary>
    /// Gets the total number of chunks in the message, present only on the first chunk.
    /// </summary>
    public int? TotalChunks { get; }

    /// <summary>
    /// Gets the checksum of the complete message, present only on the first chunk.
    /// </summary>
    public string? Checksum { get; }

    /// <summary>
    /// Creates metadata for the first chunk of a message.
    /// </summary>
    /// <param name="messageId">The unique message identifier.</param>
    /// <param name="totalChunks">The total number of chunks in the message.</param>
    /// <param name="checksum">The checksum of the complete message.</param>
    public static ChunkMetadata CreateFirstChunk(string messageId, int totalChunks, string checksum)
    {
        return new ChunkMetadata(messageId, 0, totalChunks, checksum);
    }

    /// <summary>
    /// Creates metadata for a chunk other than the first.
    /// </summary>
    /// <param name="messageId">The unique message identifier.</param>
    /// <param name="chunkIndex">The index of this chunk in the sequence.</param>
    public static ChunkMetadata CreateSubsequentChunk(string messageId, int chunkIndex)
    {
        return new ChunkMetadata(messageId, chunkIndex, null, null);
    }

    /// <summary>
    /// Formats this metadata as the value of the chunk user property.
    /// </summary>
    public string Format()
    {
        char separator = ChunkingConstants.ChunkFieldSeparator;
        string chunkIndex = ChunkIndex.ToString(CultureInfo.InvariantCulture);

        if (TotalChunks == null)
        {
            return string.Join(separator, MessageId, chunkIndex);
        }

        string totalChunks = TotalChunks.Value.ToString(CultureInfo.InvariantCulture);
        return string.Join(separator, MessageId, chunkIndex, totalChunks, Checksum);
    }

    /// <summary>
    /// Attempts to parse the value of a chunk user property.
    /// </summary>
    /// <param name="value">The user property value to parse.</param>
    /// <param name="metadata">The parsed metadata, or null if parsing failed.</param>
    /// <returns>True if the value was well formed, false otherwise.</returns>
    public static bool TryParse(string? value, out ChunkMetadata? metadata)
    {
        metadata = null;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        string[] fields = value.Split(ChunkingConstants.ChunkFieldSeparator);

        if (fields.Length is not (ChunkingConstants.FirstChunkFieldCount or ChunkingConstants.SubsequentChunkFieldCount))
        {
            return false;
        }

        if (string.IsNullOrEmpty(fields[0])
            || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int chunkIndex))
        {
            return false;
        }

        if (fields.Length == ChunkingConstants.SubsequentChunkFieldCount)
        {
            // Only the first chunk carries totalChunks and checksum, so index 0 must not use this form.
            if (chunkIndex == 0)
            {
                return false;
            }

            metadata = CreateSubsequentChunk(fields[0], chunkIndex);
            return true;
        }

        if (chunkIndex != 0
            || !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int totalChunks)
            || totalChunks < 1
            || string.IsNullOrEmpty(fields[3]))
        {
            return false;
        }

        metadata = CreateFirstChunk(fields[0], totalChunks, fields[3]);
        return true;
    }
}
