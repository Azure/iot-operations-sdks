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
    /// message-level header:
    /// <c>h:messageId:chunkIndex:totalChunks:checksumId:checksum</c>.
    /// </summary>
    public const string HeadChunkTag = "h";

    /// <summary>
    /// Tag introducing a property chunk:
    /// <c>p:messageId:chunkIndex:totalChunks</c>.
    /// </summary>
    public const string PropertyChunkTag = "p";

    /// <summary>
    /// Tag introducing a data chunk:
    /// <c>d:messageId:chunkIndex:totalChunks</c>.
    /// </summary>
    public const string DataChunkTag = "d";

    /// <summary>
    /// Number of separated fields in a <see cref="HeadChunkTag"/> value.
    /// </summary>
    public const int HeadChunkFieldCount = 6;

    /// <summary>
    /// Number of separated fields in a property or data chunk value.
    /// </summary>
    public const int BodyChunkFieldCount = 4;

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
    /// User properties copied onto every chunk in addition to their occurrence in the logical
    /// property stream.
    /// </summary>
    /// <remarks>
    /// These are the broker routing properties, so all chunks reach the same shared-subscription
    /// member and share the sender's backpressure treatment, plus the protocol version, which the
    /// receiver validates on every chunk before reassembly begins.
    /// </remarks>
    public static readonly string[] PerChunkUserProperties =
    [
        "$partition",
        "$high_priority",
        "__protVer",
        "__supProtMajVer",
    ];
}
