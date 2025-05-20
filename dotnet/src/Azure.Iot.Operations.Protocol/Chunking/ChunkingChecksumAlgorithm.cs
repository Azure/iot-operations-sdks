// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// Available checksum algorithms for chunk message integrity verification.
/// </summary>
public enum ChunkingChecksumAlgorithm
{
    /// <summary>
    /// MD5 algorithm - 128-bit hash, good performance but not cryptographically secure
    /// </summary>
    MD5,

    /// <summary>
    /// SHA-256 algorithm - 256-bit hash, cryptographically secure but larger output size
    /// </summary>
    SHA256
}
