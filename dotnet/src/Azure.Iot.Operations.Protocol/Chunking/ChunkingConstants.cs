// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.Chunking;

/// <summary>
/// Constants used for the MQTT message chunking feature.
/// </summary>
//TODO: @maximsemenov80 public for testing purposes, should be internal
public static class ChunkingConstants
{
    /// <summary>
    /// The user property name used to store chunking metadata.
    /// </summary>
    public const string ChunkUserProperty = "__chunk";

    /// <summary>
    /// JSON field name for the unique message identifier within the chunk metadata.
    /// </summary>
    public const string MessageIdField = "messageId";

    /// <summary>
    /// JSON field name for the chunk index within the chunk metadata.
    /// </summary>
    public const string ChunkIndexField = "chunkIndex";

    /// <summary>
    /// JSON field name for the timeout value within the chunk metadata.
    /// </summary>
    public const string TimeoutField = "timeout";

    /// <summary>
    /// JSON field name for the total number of chunks within the chunk metadata.
    /// </summary>
    public const string TotalChunksField = "totalChunks";

    /// <summary>
    /// JSON field name for the message checksum within the chunk metadata.
    /// </summary>
    public const string ChecksumField = "checksum";

    /// <summary>
    /// Default timeout for chunk reassembly in ISO 8601 format.
    /// </summary>
    public const string DefaultChunkTimeout = "00:00:10";

    /// <summary>
    /// Default static overhead value subtracted from the maximum packet size.
    /// This accounts for MQTT packet headers, topic name, and other metadata.
    /// </summary>
    public const int DefaultStaticOverhead = 1024;

    /// <summary>
    /// Reason string for successful chunked message transmission.
    /// </summary>
    public const string ChunkedMessageSuccessReasonString = "Chunked message successfully sent";
}
