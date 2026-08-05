// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// Constants used for the MQTT message chunking feature.
/// </summary>
internal static class ChunkingConstants
{
    /// <summary>
    /// The user property name used to store chunking metadata.
    /// </summary>
    public const string ChunkUserProperty = "__chunk";

    /// <summary>
    /// Separator between the fields of the <see cref="ChunkUserProperty"/> value.
    /// </summary>
    public const char ChunkFieldSeparator = ':';

    /// <summary>
    /// Number of separated fields carried by a first chunk: messageId, chunkIndex, totalChunks, checksum.
    /// </summary>
    public const int FirstChunkFieldCount = 4;

    /// <summary>
    /// Number of separated fields carried by a subsequent chunk: messageId, chunkIndex.
    /// </summary>
    public const int SubsequentChunkFieldCount = 2;

    /// <summary>
    /// Default static overhead value subtracted from the maximum packet size.
    /// This accounts for MQTT packet headers, topic name, and other metadata.
    /// </summary>
    public const int DefaultStaticOverhead = 1024;

    /// <summary>
    /// Stand-in for the broker-negotiated maximum packet size, which is not reachable from the
    /// protocol envoys today.
    /// </summary>
    /// <remarks>
    /// POC only. Deliberately conservative so it stays below any realistic broker limit. See gap
    /// G1 in doc/dev/rpc-chunking-working-doc.md for what has to replace this.
    /// </remarks>
    public const int PlaceholderMaxPacketSize = 64 * 1024;

    /// <summary>
    /// User properties copied onto every chunk rather than the first one alone.
    /// </summary>
    /// <remarks>
    /// Per ADR 0023 the first chunk carries the full property set and later chunks carry only what
    /// is needed to deliver and reassemble the message. That means the broker routing properties,
    /// so all chunks reach the same shared-subscription member and share the sender's backpressure
    /// treatment, plus the protocol version, which the receiver validates on every chunk before
    /// reassembly begins.
    /// </remarks>
    public static readonly string[] PerChunkUserProperties =
    [
        "$partition",
        "$high_priority",
        "__protVer",
    ];
}
