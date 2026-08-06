// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Security.Cryptography;

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// Provides checksum calculation for message chunking.
/// </summary>
internal static class ChecksumCalculator
{
    /// <summary>
    /// Calculates a checksum for the given data using the specified algorithm.
    /// </summary>
    /// <param name="data">The data to calculate a checksum for.</param>
    /// <param name="algorithm">The algorithm to use for the checksum.</param>
    /// <returns>A string representation of the checksum.</returns>
    public static string CalculateChecksum(ReadOnlySequence<byte> data, ChunkingChecksumAlgorithm algorithm)
    {
        ReadOnlySpan<byte> hash = CalculateHashBytes(data, algorithm);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Verifies that the calculated checksum matches the expected checksum.
    /// </summary>
    /// <param name="data">The data to calculate a checksum for.</param>
    /// <param name="expectedChecksum">The expected checksum value.</param>
    /// <param name="algorithm">The algorithm to use for the checksum.</param>
    /// <returns>True if the checksums match, false otherwise.</returns>
    public static bool VerifyChecksum(ReadOnlySequence<byte> data, string expectedChecksum, ChunkingChecksumAlgorithm algorithm)
    {
        string actualChecksum = CalculateChecksum(data, algorithm);
        return string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] CalculateHashBytes(ReadOnlySequence<byte> data, ChunkingChecksumAlgorithm algorithm)
    {
        if (data.IsSingleSegment)
        {
            return algorithm switch
            {
#pragma warning disable CA5351 // Not a security control: the checksum guards against reassembly bugs, not tampering.
                ChunkingChecksumAlgorithm.MD5 => MD5.HashData(data.FirstSpan),
#pragma warning restore CA5351
                ChunkingChecksumAlgorithm.SHA256 => SHA256.HashData(data.FirstSpan),
                _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
            };
        }

        using IncrementalHash hashAlgorithm = IncrementalHash.CreateHash(HashAlgorithmNameFor(algorithm));

        foreach (ReadOnlyMemory<byte> segment in data)
        {
            hashAlgorithm.AppendData(segment.Span);
        }

        return hashAlgorithm.GetHashAndReset();
    }

    private static HashAlgorithmName HashAlgorithmNameFor(ChunkingChecksumAlgorithm algorithm)
    {
        return algorithm switch
        {
            ChunkingChecksumAlgorithm.MD5 => HashAlgorithmName.MD5,
            ChunkingChecksumAlgorithm.SHA256 => HashAlgorithmName.SHA256,
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
        };
    }
}
