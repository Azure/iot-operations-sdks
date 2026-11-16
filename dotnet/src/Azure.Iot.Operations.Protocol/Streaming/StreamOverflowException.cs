// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// A consumer's stream buffer was full when another entry arrived (ADR 25, flow control). The consumer ends the
    /// exchange with an <c>Overflow</c> status (<c>__stat</c> 429); its application receives this error after the buffered
    /// entries, and the producer's application receives it with <see cref="IsRemote"/> set.
    /// </summary>
    public class StreamOverflowException : Exception
    {
        public StreamOverflowException(uint index, string? message = null)
            : base(message ?? $"The stream buffer was full when entry {index} arrived.")
        {
            Index = index;
        }

        /// <summary>The index of the entry that did not fit in the consumer's buffer.</summary>
        public uint Index { get; }

        /// <summary><c>true</c> when the overflow happened at the peer rather than locally.</summary>
        public bool IsRemote { get; init; }
    }
}
