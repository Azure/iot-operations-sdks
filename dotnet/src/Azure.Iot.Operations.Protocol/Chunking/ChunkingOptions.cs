// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.Chunking;

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
    /// Gets or sets the checksum algorithm to use for message integrity verification.
    /// </summary>
    public ChunkingChecksumAlgorithm ChecksumAlgorithm { get; set; } = ChunkingChecksumAlgorithm.SHA256;
}
