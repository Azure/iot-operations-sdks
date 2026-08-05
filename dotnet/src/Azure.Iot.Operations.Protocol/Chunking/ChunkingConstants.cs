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
    /// Tag introducing a head chunk, the first chunk of a message, which additionally carries the
    /// message-level header: <c>h:messageId:chunkIndex:totalChunks:checksum</c>.
    /// </summary>
    public const string HeadChunkTag = "h";

    /// <summary>
    /// Tag introducing a data chunk, any chunk after the first:
    /// <c>d:messageId:chunkIndex</c>.
    /// </summary>
    public const string DataChunkTag = "d";

    /// <summary>
    /// Number of separated fields in a <see cref="HeadChunkTag"/> value.
    /// </summary>
    public const int HeadChunkFieldCount = 5;

    /// <summary>
    /// Number of separated fields in a <see cref="DataChunkTag"/> value.
    /// </summary>
    public const int DataChunkFieldCount = 3;

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
