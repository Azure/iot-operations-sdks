// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// Configuration options for the MQTT message chunking feature.
/// </summary>
/// <remarks>
/// Chunking is automatic and opaque to the application, so there is deliberately no enable/disable
/// switch here.
/// </remarks>
internal class ChunkingOptions
{
    /// <summary>
    /// Gets or sets the static overhead value subtracted from the MQTT maximum packet size
    /// to account for headers, topic names, and other metadata.
    /// </summary>
    public int StaticOverhead { get; set; } = ChunkingConstants.DefaultStaticOverhead;

    /// <summary>
    /// Gets or sets the checksum algorithm to use for message integrity verification.
    /// </summary>
    public ChunkingChecksumAlgorithm ChecksumAlgorithm { get; set; } = ChunkingChecksumAlgorithm.SHA256;

    /// <summary>
    /// Gets or sets the maximum total size (in bytes) of all chunk payloads that can be buffered
    /// simultaneously during message reassembly. When this limit is exceeded, new chunks will be rejected.
    /// A value of 0 or negative means no limit.
    /// </summary>
    public long ReassemblyBufferSizeLimit { get; set; } = 10 * 1024 * 1024; // 10 MB default
}
