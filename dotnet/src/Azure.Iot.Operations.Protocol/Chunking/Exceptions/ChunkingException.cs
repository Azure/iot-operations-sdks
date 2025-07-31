// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Chunking.Exceptions;

/// <summary>
/// Base exception class for all chunking-related errors.
/// </summary>
public abstract class ChunkingException : Exception
{
    /// <summary>
    /// Gets the message ID associated with the chunked message that caused the error.
    /// </summary>
    public string MessageId { get; }

    /// <summary>
    /// Gets the chunk index that caused the error, if applicable.
    /// </summary>
    public int? ChunkIndex { get; }

    /// <summary>
    /// Gets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkingException"/> class.
    /// </summary>
    /// <param name="messageId">The message ID associated with the error.</param>
    /// <param name="message">The error message.</param>
    /// <param name="chunkIndex">The chunk index that caused the error, if applicable.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    protected ChunkingException(string messageId, string message, int? chunkIndex = null, Exception? innerException = null)
        : base(message, innerException)
    {
        MessageId = messageId ?? throw new ArgumentNullException(nameof(messageId));
        ChunkIndex = chunkIndex;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns a string that represents the current exception.
    /// </summary>
    /// <returns>A string representation of the exception.</returns>
    public override string ToString()
    {
        var result = $"{GetType().Name}: {Message} (MessageId: {MessageId}";
        
        if (ChunkIndex.HasValue)
        {
            result += $", ChunkIndex: {ChunkIndex.Value}";
        }
        
        result += $", Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss} UTC)";
        
        if (InnerException != null)
        {
            result += $"\n ---> {InnerException}";
        }
        
        return result;
    }
}
