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
/// colon-separated string introduced by a tag that determines the form, mirroring the streaming
/// protocol's <c>__stream</c> property:
/// <code>
/// chunk_metadata ::= head_chunk | data_chunk
/// head_chunk     ::= "h" ":" message_id ":" chunk_index ":" total_chunks ":" checksum_id ":" checksum
/// data_chunk     ::= "d" ":" message_id ":" chunk_index
/// </code>
/// A head chunk is always index 0 and is the only one carrying the message-level header, so a
/// message only ever carries the fields that apply to it and the parser never has to infer the
/// form from how many fields arrived.
/// </remarks>
internal sealed class ChunkMetadata
{
    private ChunkMetadata(string messageId, int chunkIndex, int? totalChunks, string? checksumId, string? checksum)
    {
        MessageId = messageId;
        ChunkIndex = chunkIndex;
        TotalChunks = totalChunks;
        ChecksumId = checksumId;
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
    /// Gets the identifier of the algorithm that produced <see cref="Checksum"/>, present only on
    /// the first chunk. Without it a receiver configured differently from the sender would verify
    /// with the wrong algorithm and report a mismatch indistinguishable from corruption.
    /// </summary>
    public string? ChecksumId { get; }

    /// <summary>
    /// Gets the checksum of the complete message, present only on the first chunk.
    /// </summary>
    public string? Checksum { get; }

    /// <summary>
    /// Creates metadata for the first chunk of a message.
    /// </summary>
    /// <param name="messageId">The unique message identifier.</param>
    /// <param name="totalChunks">The total number of chunks in the message.</param>
    /// <param name="checksumId">The identifier of the algorithm that produced the checksum.</param>
    /// <param name="checksum">The checksum of the complete message.</param>
    public static ChunkMetadata CreateFirstChunk(string messageId, int totalChunks, string checksumId, string checksum)
    {
        return new ChunkMetadata(messageId, 0, totalChunks, checksumId, checksum);
    }

    /// <summary>
    /// Creates metadata for a chunk other than the first.
    /// </summary>
    /// <param name="messageId">The unique message identifier.</param>
    /// <param name="chunkIndex">The index of this chunk in the sequence.</param>
    public static ChunkMetadata CreateSubsequentChunk(string messageId, int chunkIndex)
    {
        return new ChunkMetadata(messageId, chunkIndex, null, null, null);
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
            return string.Join(separator, ChunkingConstants.DataChunkTag, MessageId, chunkIndex);
        }

        string totalChunks = TotalChunks.Value.ToString(CultureInfo.InvariantCulture);
        return string.Join(separator, ChunkingConstants.HeadChunkTag, MessageId, chunkIndex, totalChunks, ChecksumId, Checksum);
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

        // The leading tag alone determines the form, so each branch knows exactly which fields to
        // expect rather than inferring them from how many arrived.
        return fields[0] switch
        {
            ChunkingConstants.HeadChunkTag => TryParseHeadChunk(fields, out metadata),
            ChunkingConstants.DataChunkTag => TryParseDataChunk(fields, out metadata),
            _ => false,
        };
    }

    private static bool TryParseHeadChunk(string[] fields, out ChunkMetadata? metadata)
    {
        metadata = null;

        if (fields.Length != ChunkingConstants.HeadChunkFieldCount
            || string.IsNullOrEmpty(fields[1])
            || !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int chunkIndex)
            || chunkIndex != 0
            || !int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out int totalChunks)
            || totalChunks < 1
            || string.IsNullOrEmpty(fields[4])
            || string.IsNullOrEmpty(fields[5]))
        {
            return false;
        }

        metadata = CreateFirstChunk(fields[1], totalChunks, fields[4], fields[5]);
        return true;
    }

    private static bool TryParseDataChunk(string[] fields, out ChunkMetadata? metadata)
    {
        metadata = null;

        // Index 0 is the head chunk, which must use the head form.
        if (fields.Length != ChunkingConstants.DataChunkFieldCount
            || string.IsNullOrEmpty(fields[1])
            || !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int chunkIndex)
            || chunkIndex < 1)
        {
            return false;
        }

        metadata = CreateSubsequentChunk(fields[1], chunkIndex);
        return true;
    }
}
