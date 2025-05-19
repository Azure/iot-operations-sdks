// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Security.Cryptography;

namespace Azure.Iot.Operations.Protocol.Chunking
{
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
            using HashAlgorithm hashAlgorithm = CreateHashAlgorithm(algorithm);

            if (data.IsSingleSegment)
            {
                return hashAlgorithm.ComputeHash(data.FirstSpan.ToArray());
            }
            else
            {
                // Process multiple segments
                hashAlgorithm.Initialize();

                foreach (ReadOnlyMemory<byte> segment in data)
                {
                    hashAlgorithm.TransformBlock(segment.Span.ToArray(), 0, segment.Length, null, 0);
                }

                hashAlgorithm.TransformFinalBlock([], 0, 0);
                return hashAlgorithm.Hash!;
            }
        }

        private static HashAlgorithm CreateHashAlgorithm(ChunkingChecksumAlgorithm algorithm)
        {
            return algorithm switch
            {
                ChunkingChecksumAlgorithm.MD5 => MD5.Create(),
                ChunkingChecksumAlgorithm.SHA256 => SHA256.Create(),
                _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, null)
            };
        }
    }
}
