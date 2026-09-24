// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Security.Cryptography;

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// SHA-256 chunk checksum, the default.
/// </summary>
/// <remarks>
/// Chosen for speed rather than for its cryptographic properties, which chunking does not rely on:
/// on hardware with SHA extensions it outruns a byte-at-a-time CRC32 several times over. On a
/// target without those extensions, measure before assuming it is still the right pick.
/// </remarks>
internal sealed class Sha256ChunkChecksum : IChunkChecksum
{
    public string Id => "sha256";

    public string Compute(ReadOnlySequence<byte> payload)
    {
        if (payload.IsSingleSegment)
        {
            return Convert.ToHexString(SHA256.HashData(payload.FirstSpan)).ToLowerInvariant();
        }

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (ReadOnlyMemory<byte> segment in payload)
        {
            hash.AppendData(segment.Span);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
