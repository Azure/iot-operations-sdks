// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Azure.Iot.Operations.Protocol.Chunking.Exceptions;

/// <summary>
/// Exception thrown when chunk assembly fails due to malformed chunks or other assembly issues.
/// </summary>
public class ChunkAssemblyError : ChunkingException
{
    /// <summary>
    /// Gets detailed error information about the assembly failure.
    /// </summary>
    public string ErrorDetails { get; }

    /// <summary>
    /// Gets the type of assembly error that occurred.
    /// </summary>
    public ChunkAssemblyErrorType ErrorType { get; }

    /// <summary>
    /// Gets additional context about the assembly state when the error occurred.
    /// </summary>
    public Dictionary<string, object> Context { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkAssemblyError"/> class.
    /// </summary>
    /// <param name="messageId">The message ID that failed to assemble.</param>
    /// <param name="chunkIndex">The chunk index where the error occurred, if applicable.</param>
    /// <param name="errorType">The type of assembly error.</param>
    /// <param name="errorDetails">Detailed error information.</param>
    /// <param name="context">Additional context about the assembly state.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    public ChunkAssemblyError(
        string messageId,
        int? chunkIndex,
        ChunkAssemblyErrorType errorType,
        string errorDetails,
        Dictionary<string, object>? context = null,
        Exception? innerException = null)
        : base(messageId,
               $"Chunk assembly failed: {errorType}. {errorDetails}",
               chunkIndex,
               innerException)
    {
        ErrorDetails = errorDetails ?? throw new ArgumentNullException(nameof(errorDetails));
        ErrorType = errorType;
        Context = context ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Creates a ChunkAssemblyError for malformed chunk metadata.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="chunkIndex">The chunk index.</param>
    /// <param name="metadataIssue">Description of the metadata issue.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    /// <returns>A new ChunkAssemblyError instance.</returns>
    public static ChunkAssemblyError MalformedMetadata(string messageId, int chunkIndex, string metadataIssue, Exception? innerException = null)
    {
        return new ChunkAssemblyError(
            messageId,
            chunkIndex,
            ChunkAssemblyErrorType.MalformedMetadata,
            $"Chunk metadata is malformed: {metadataIssue}",
            new Dictionary<string, object> { { "MetadataIssue", metadataIssue } },
            innerException);
    }

    /// <summary>
    /// Creates a ChunkAssemblyError for duplicate chunks.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="chunkIndex">The duplicate chunk index.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    /// <returns>A new ChunkAssemblyError instance.</returns>
    public static ChunkAssemblyError DuplicateChunk(string messageId, int chunkIndex, Exception? innerException = null)
    {
        return new ChunkAssemblyError(
            messageId,
            chunkIndex,
            ChunkAssemblyErrorType.DuplicateChunk,
            $"Duplicate chunk received for index {chunkIndex}",
            new Dictionary<string, object> { { "DuplicateIndex", chunkIndex } },
            innerException);
    }

    /// <summary>
    /// Creates a ChunkAssemblyError for invalid chunk order.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="chunkIndex">The out-of-order chunk index.</param>
    /// <param name="expectedRange">The expected chunk index range.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    /// <returns>A new ChunkAssemblyError instance.</returns>
    public static ChunkAssemblyError InvalidChunkOrder(string messageId, int chunkIndex, string expectedRange, Exception? innerException = null)
    {
        return new ChunkAssemblyError(
            messageId,
            chunkIndex,
            ChunkAssemblyErrorType.InvalidChunkOrder,
            $"Chunk index {chunkIndex} is outside expected range: {expectedRange}",
            new Dictionary<string, object> 
            { 
                { "ChunkIndex", chunkIndex },
                { "ExpectedRange", expectedRange }
            },
            innerException);
    }

    /// <summary>
    /// Creates a ChunkAssemblyError for payload serialization failures.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="serializationError">Description of the serialization error.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    /// <returns>A new ChunkAssemblyError instance.</returns>
    public static ChunkAssemblyError PayloadSerialization(string messageId, string serializationError, Exception? innerException = null)
    {
        return new ChunkAssemblyError(
            messageId,
            null,
            ChunkAssemblyErrorType.PayloadSerialization,
            $"Failed to serialize reassembled payload: {serializationError}",
            new Dictionary<string, object> { { "SerializationError", serializationError } },
            innerException);
    }
}

/// <summary>
/// Defines the types of chunk assembly errors that can occur.
/// </summary>
public enum ChunkAssemblyErrorType
{
    /// <summary>
    /// The chunk metadata is malformed or invalid.
    /// </summary>
    MalformedMetadata,

    /// <summary>
    /// A duplicate chunk was received.
    /// </summary>
    DuplicateChunk,

    /// <summary>
    /// Chunks were received in an invalid order or with invalid indices.
    /// </summary>
    InvalidChunkOrder,

    /// <summary>
    /// Failed to serialize the reassembled payload.
    /// </summary>
    PayloadSerialization,

    /// <summary>
    /// A general assembly error occurred.
    /// </summary>
    General
}
