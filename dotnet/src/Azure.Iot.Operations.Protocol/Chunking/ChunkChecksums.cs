// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// The chunk checksums this SDK ships with, and the lookup a receiver uses to match the algorithm
/// named on the wire.
/// </summary>
internal static class ChunkChecksums
{
    public static IChunkChecksum Sha256 { get; } = new Sha256ChunkChecksum();

    private static readonly Dictionary<string, IChunkChecksum> BuiltIn =
        new(StringComparer.Ordinal) { [Sha256.Id] = Sha256 };

    /// <summary>
    /// Finds the built-in checksum with the given identifier, or null if there is none.
    /// </summary>
    /// <remarks>
    /// Returning null rather than falling back to a default is deliberate: verifying with an
    /// algorithm other than the one the sender used would report a mismatch that looks exactly like
    /// data corruption.
    /// </remarks>
    public static IChunkChecksum? Resolve(string id)
    {
        return id != null && BuiltIn.TryGetValue(id, out IChunkChecksum? checksum) ? checksum : null;
    }
}
