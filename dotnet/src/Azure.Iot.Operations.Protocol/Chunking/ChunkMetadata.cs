// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Linq;

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// Represents the metadata for a chunk of a larger MQTT message.
/// </summary>
/// <remarks>
/// Serialized into the <see cref="ChunkingConstants.ChunkUserProperty"/> user property as a
/// colon-separated string introduced by a tag that determines the form, mirroring the streaming
/// protocol's <c>__stream</c> property:
/// <code>
/// chunk_metadata ::= head_chunk | property_chunk | data_chunk
/// head_chunk     ::= "h" ":" message_id ":" chunk_index ":" total_chunks ":" checksum_id ":" checksum
/// property_chunk ::= "p" ":" message_id ":" chunk_index ":" total_chunks
/// data_chunk     ::= "d" ":" message_id ":" chunk_index ":" total_chunks
/// </code>
/// A head chunk is always index 0 and is the only one carrying checksum metadata. The leading tag
/// determines the chunk's role without inferring it from the field count.
/// </remarks>
internal sealed class ChunkMetadata
{
    private ChunkMetadata(
        ChunkKind kind,
        string messageId,
        int chunkIndex,
        int totalChunks,
        string? checksumId,
        string? checksum,
        uint? remainingSeconds = null)
    {
        Kind = kind;
        MessageId = messageId;
        ChunkIndex = chunkIndex;
        TotalChunks = totalChunks;
        ChecksumId = checksumId;
        Checksum = checksum;
        RemainingSeconds = remainingSeconds;
    }

    /// <summary>
    /// Gets the role of this chunk in the reassembled message.
    /// </summary>
    public ChunkKind Kind { get; }

    /// <summary>
    /// Gets the unique identifier for the chunked message.
    /// </summary>
    public string MessageId { get; }

    /// <summary>
    /// Gets the index of this chunk in the sequence.
    /// </summary>
    public int ChunkIndex { get; }

    /// <summary>
    /// Gets the total number of chunks in the message.
    /// </summary>
    public int TotalChunks { get; }

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
    /// Gets the sender's remaining operation budget, when carried by a request chunk.
    /// </summary>
    public uint? RemainingSeconds { get; }

    /// <summary>
    /// Creates metadata for the first chunk of a message.
    /// </summary>
    /// <param name="messageId">The unique message identifier.</param>
    /// <param name="totalChunks">The total number of chunks in the message.</param>
    /// <param name="checksumId">The identifier of the algorithm that produced the checksum.</param>
    /// <param name="checksum">The checksum of the complete message.</param>
    public static ChunkMetadata CreateFirstChunk(string messageId, int totalChunks, string checksumId, string checksum)
    {
        return new ChunkMetadata(ChunkKind.Head, messageId, 0, totalChunks, checksumId, checksum);
    }

    /// <summary>
    /// Creates metadata for a property chunk.
    /// </summary>
    /// <param name="messageId">The unique message identifier.</param>
    /// <param name="chunkIndex">The index of this chunk in the sequence.</param>
    /// <param name="totalChunks">The total number of chunks in the message.</param>
    public static ChunkMetadata CreatePropertyChunk(string messageId, int chunkIndex, int totalChunks)
    {
        return new ChunkMetadata(ChunkKind.Property, messageId, chunkIndex, totalChunks, null, null);
    }

    /// <summary>
    /// Creates metadata for a data chunk.
    /// </summary>
    /// <param name="messageId">The unique message identifier.</param>
    /// <param name="chunkIndex">The index of this chunk in the sequence.</param>
    /// <param name="totalChunks">The total number of chunks in the message.</param>
    public static ChunkMetadata CreateDataChunk(string messageId, int chunkIndex, int totalChunks)
    {
        return new ChunkMetadata(ChunkKind.Data, messageId, chunkIndex, totalChunks, null, null);
    }

    /// <summary>
    /// Formats this metadata as the value of the chunk user property.
    /// </summary>
    public string Format(uint? remainingSeconds = null)
    {
        remainingSeconds ??= RemainingSeconds;
        if (remainingSeconds == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingSeconds));
        }

        char separator = ChunkingConstants.ChunkFieldSeparator;
        if (Kind == ChunkKind.Head)
        {
            if (string.IsNullOrEmpty(ChecksumId) || ChecksumId.Contains(separator))
            {
                throw new ArgumentException($"Checksum ID must be non-empty and must not contain '{separator}'.");
            }

            if (!IsLowercaseHex(Checksum ?? string.Empty))
            {
                throw new ArgumentException("Checksum must be a non-empty lowercase hexadecimal string.");
            }
        }

        string chunkIndex = ChunkIndex.ToString(CultureInfo.InvariantCulture);
        string totalChunks = TotalChunks.ToString(CultureInfo.InvariantCulture);

        string value = Kind switch
        {
            ChunkKind.Head => string.Join(separator, ChunkingConstants.HeadChunkTag, MessageId, chunkIndex, totalChunks, ChecksumId, Checksum),
            ChunkKind.Property => string.Join(separator, ChunkingConstants.PropertyChunkTag, MessageId, chunkIndex, totalChunks),
            _ => string.Join(separator, ChunkingConstants.DataChunkTag, MessageId, chunkIndex, totalChunks),
        };

        return remainingSeconds.HasValue
            ? string.Join(separator, value, remainingSeconds.Value.ToString(CultureInfo.InvariantCulture))
            : value;
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
            ChunkingConstants.PropertyChunkTag => TryParseBodyChunk(fields, ChunkKind.Property, out metadata),
            ChunkingConstants.DataChunkTag => TryParseBodyChunk(fields, ChunkKind.Data, out metadata),
            _ => false,
        };
    }

    private static bool TryParseHeadChunk(string[] fields, out ChunkMetadata? metadata)
    {
        metadata = null;

        if (fields.Length is not ChunkingConstants.HeadChunkFieldCount and not (ChunkingConstants.HeadChunkFieldCount + 1)
            || !IsCanonicalMessageId(fields[1])
            || !TryParseCanonicalNonNegativeInt(fields[2], out int chunkIndex)
            || chunkIndex != 0
            || !TryParseCanonicalPositiveInt(fields[3], out int totalChunks)
            || string.IsNullOrEmpty(fields[4])
            || !IsLowercaseHex(fields[5])
            || !TryParseRemainingSeconds(fields, ChunkingConstants.HeadChunkFieldCount, out uint? remainingSeconds))
        {
            return false;
        }

        metadata = new(ChunkKind.Head, fields[1], 0, totalChunks, fields[4], fields[5], remainingSeconds);
        return true;
    }

    private static bool TryParseBodyChunk(string[] fields, ChunkKind kind, out ChunkMetadata? metadata)
    {
        metadata = null;

        // Index 0 is the head chunk, which must use the head form.
        if (fields.Length is not ChunkingConstants.BodyChunkFieldCount and not (ChunkingConstants.BodyChunkFieldCount + 1)
            || !IsCanonicalMessageId(fields[1])
            || !TryParseCanonicalPositiveInt(fields[2], out int chunkIndex)
            || chunkIndex < 1
            || !TryParseCanonicalPositiveInt(fields[3], out int totalChunks)
            || chunkIndex >= totalChunks
            || !TryParseRemainingSeconds(fields, ChunkingConstants.BodyChunkFieldCount, out uint? remainingSeconds))
        {
            return false;
        }

        metadata = new(kind, fields[1], chunkIndex, totalChunks, null, null, remainingSeconds);
        return true;
    }

    private static bool IsCanonicalMessageId(string value) =>
        Guid.TryParseExact(value, "D", out Guid messageId)
        && string.Equals(value, messageId.ToString("D"), StringComparison.Ordinal);

    private static bool IsLowercaseHex(string value) =>
        value.Length > 0 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool TryParseCanonicalNonNegativeInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result)
        && string.Equals(value, result.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private static bool TryParseCanonicalPositiveInt(string value, out int result) =>
        TryParseCanonicalNonNegativeInt(value, out result) && result > 0;

    private static bool TryParseRemainingSeconds(string[] fields, int baseFieldCount, out uint? remainingSeconds)
    {
        remainingSeconds = null;
        if (fields.Length == baseFieldCount)
        {
            return true;
        }

        if (!uint.TryParse(fields[^1], NumberStyles.None, CultureInfo.InvariantCulture, out uint parsed)
            || parsed == 0
            || !string.Equals(fields[^1], parsed.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            return false;
        }

        remainingSeconds = parsed;
        return true;
    }
}

internal enum ChunkKind
{
    Head,
    Property,
    Data,
}
