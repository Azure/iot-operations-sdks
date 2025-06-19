// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// Represents the metadata for a chunk of a larger MQTT message.
/// </summary>
internal class ChunkMetadata
{
    /// <summary>
    /// Gets or sets the unique identifier for the chunked message.
    /// </summary>
    [JsonPropertyName(ChunkingConstants.MessageIdField)]
    public string MessageId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the index of this chunk in the sequence.
    /// </summary>
    [JsonPropertyName(ChunkingConstants.ChunkIndexField)]
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Gets or sets the total number of chunks in the message.
    /// This property is only present in the first chunk.
    /// </summary>
    [JsonPropertyName(ChunkingConstants.TotalChunksField)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalChunks { get; set; }

    /// <summary>
    /// Gets or sets the checksum of the complete message.
    /// This property is only present in the first chunk.
    /// </summary>
    [JsonPropertyName(ChunkingConstants.ChecksumField)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Checksum { get; set; }

    /// <summary>
    /// Creates a new instance of the <see cref="ChunkMetadata"/> class for a first chunk.
    /// </summary>
    /// <param name="messageId">The unique message identifier.</param>
    /// <param name="totalChunks">The total number of chunks in the message.</param>
    /// <param name="checksum">The checksum of the complete message.</param>
    /// <returns>A new instance of <see cref="ChunkMetadata"/> configured for the first chunk.</returns>
    public static ChunkMetadata CreateFirstChunk(string messageId, int totalChunks, string checksum)
    {
        return new ChunkMetadata
        {
            MessageId = messageId,
            ChunkIndex = 0,
            TotalChunks = totalChunks,
            Checksum = checksum
        };
    }    /// <summary>
    /// Creates a new instance of the <see cref="ChunkMetadata"/> class for subsequent chunks.
    /// </summary>
    /// <param name="messageId">The unique message identifier.</param>
    /// <param name="chunkIndex">The index of this chunk in the sequence.</param>
    /// <returns>A new instance of <see cref="ChunkMetadata"/> configured for a subsequent chunk.</returns>
    public static ChunkMetadata CreateSubsequentChunk(string messageId, int chunkIndex)
    {
        return new ChunkMetadata
        {
            MessageId = messageId,
            ChunkIndex = chunkIndex
        };
    }
}
