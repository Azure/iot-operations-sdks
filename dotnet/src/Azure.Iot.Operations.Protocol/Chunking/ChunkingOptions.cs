// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

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
    /// Gets or sets the checksum used when splitting a message. Its
    /// <see cref="IChunkChecksum.Id"/> travels on the head chunk.
    /// </summary>
    public IChunkChecksum Checksum { get; set; } = ChunkChecksums.Sha256;

    /// <summary>
    /// Gets or sets the lookup used when reassembling, mapping the identifier on the head chunk to
    /// an implementation. Returning null discards the message rather than verifying it with the
    /// wrong algorithm. Replace this to accept a custom <see cref="IChunkChecksum"/>.
    /// </summary>
    public Func<string, IChunkChecksum?> ResolveChecksum { get; set; } = ChunkChecksums.Resolve;

    /// <summary>
    /// Gets or sets the maximum number of chunks a single message may be split into.
    /// </summary>
    /// <remarks>
    /// Bounds both reassembly memory and MQTT packet identifier consumption, since no chunk is
    /// acknowledged until the whole message has been reassembled.
    /// </remarks>
    public int MaxChunkCount { get; set; } = 100;

    /// <summary>
    /// Gets or sets the longest time a partial message may hold resources locally, regardless of a
    /// peer-declared operation countdown.
    /// </summary>
    public TimeSpan MaxReassemblyWindow { get; set; } = TimeSpan.FromHours(1);
}
