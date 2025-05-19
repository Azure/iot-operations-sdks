// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;

namespace Azure.Iot.Operations.Protocol.Chunking
{
    /// <summary>
    /// Configuration options for the MQTT message chunking feature.
    /// </summary>
    public class ChunkingOptions
    {
        /// <summary>
        /// Gets or sets whether chunking is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the static overhead value subtracted from the MQTT maximum packet size
        /// to account for headers, topic names, and other metadata.
        /// </summary>
        public int StaticOverhead { get; set; } = ChunkingConstants.DefaultStaticOverhead;

        /// <summary>
        /// Gets or sets the timeout duration for reassembling chunked messages.
        /// </summary>
        public TimeSpan ChunkTimeout { get; set; } = TimeSpan.Parse(ChunkingConstants.DefaultChunkTimeout, CultureInfo.InvariantCulture);
        /// <summary>
        /// Gets or sets the maximum time to wait for all chunks to arrive.
        /// </summary>
        public TimeSpan MaxReassemblyTime { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Gets or sets the checksum algorithm to use for message integrity verification.
        /// </summary>
        public ChunkingChecksumAlgorithm ChecksumAlgorithm { get; set; } = ChunkingChecksumAlgorithm.SHA256;
    }

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
}
