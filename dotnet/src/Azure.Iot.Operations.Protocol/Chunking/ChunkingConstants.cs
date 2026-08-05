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
}
