// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Resource entity.
/// </summary>
public class CoreResourceEntity
{
    /// <summary>
    /// Resource identifier.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Full XID path.
    /// </summary>
    public required string XId { get; set; }

    /// <summary>
    /// An object that contains most of the Resource-level attributes.
    /// </summary>
    public required CoreResourceMeta Meta { get; set; }

    /// <summary>
    /// A specific Version of a Resource.
    /// </summary>
    public required CoreVersionEntity DefaultVersion { get; set; }

    /// <summary>
    /// The number of Versions contained on the Resource.
    /// </summary>
    public required ulong VersionsCount { get; set; }

    /// <summary>
    /// Extension-specific attributes (e.g., `format` and `content_type` for schemas).
    /// </summary>
    public required Dictionary<string, byte[]> Extensions { get; set; }
}
