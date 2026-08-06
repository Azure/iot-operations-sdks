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
    /// Gets or sets the safety margin, in bytes, left free in each data chunk on top of its
    /// measured overhead.
    /// </summary>
    /// <remarks>
    /// A data chunk's overhead is measured rather than guessed, by sizing an empty chunk, but
    /// <see cref="MqttPacketSizeEstimator"/> cannot perfectly predict how the underlying client
    /// encodes a packet, so a small margin is kept.
    /// </remarks>
    public int StaticOverhead { get; set; } = ChunkingConstants.DefaultSafetyMargin;

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

    /// <summary>
    /// Gets or sets the maximum number of chunks a single message may be split into.
    /// </summary>
    /// <remarks>
    /// Bounds both reassembly memory and MQTT packet identifier consumption, since no chunk is
    /// acknowledged until the whole message has been reassembled.
    /// </remarks>
    public int MaxChunkCount { get; set; } = 100;
}
