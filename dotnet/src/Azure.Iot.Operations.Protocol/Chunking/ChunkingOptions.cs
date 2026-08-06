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
    /// calculated overhead.
    /// </summary>
    /// <remarks>
    /// <see cref="MqttPacketSizeCalculator"/> is exact, so this is not covering arithmetic error.
    /// It anticipates what a broker may add between publish and delivery, chiefly a subscription
    /// identifier per matching subscription, since a packet too large to deliver is discarded
    /// silently and a vanished chunk stalls reassembly. Nothing consumes it today: the SDK sets no
    /// subscription identifiers and no topic aliases, so publish and delivery sizes are equal.
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
