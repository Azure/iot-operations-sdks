// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.Chunking.Exceptions;

/// <summary>
/// Thrown when a message cannot be represented within the configured packet and chunk limits.
/// </summary>
internal sealed class MessageTooLargeError : ChunkingException
{
    public MessageTooLargeError(string messageId, string message)
        : base(messageId, message)
    {
    }
}