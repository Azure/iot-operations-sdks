// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Buffers;

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// Computes the integrity check carried on a chunked message's head chunk.
/// </summary>
/// <remarks>
/// This guards against reassembly going wrong — a splitter or assembler bug producing a complete
/// but incorrect payload, or two language implementations disagreeing about how to split. It is
/// deliberately <b>not</b> a security control: the value travels unauthenticated in a user property
/// beside the payload it describes, so anything able to alter the payload can recompute it. Choose
/// an implementation for speed on the target hardware, not for cryptographic strength.
/// </remarks>
internal interface IChunkChecksum
{
    /// <summary>
    /// Gets the identifier written to the head chunk so the receiver verifies with the same
    /// algorithm the sender used. Must not contain <see cref="ChunkingConstants.ChunkFieldSeparator"/>.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Computes the checksum of a complete payload as a non-empty lowercase hexadecimal string.
    /// </summary>
    string Compute(ReadOnlySequence<byte> payload);
}
