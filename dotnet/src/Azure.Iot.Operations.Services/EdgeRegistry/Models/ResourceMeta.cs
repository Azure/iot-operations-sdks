// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// The server-managed metadata sub-entity of a Resource.
/// </summary>
public class ResourceMeta
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
    /// Indicates that this Resource is a reference to another Resource within the same Registry. The XID path of the referenced Resource.
    /// </summary>
    public string? XRef { get; set; }

    /// <summary>
    /// A numeric value used to determine whether an entity has been modified.
    /// </summary>
    public required ulong Epoch { get; set; }

    /// <summary>
    /// A mechanism in which additional metadata about the entity can be stored without changing the model definition of the entity. Labels can be used for querying.
    /// </summary>
    public required List<Label> Labels { get; set; }

    /// <summary>
    /// The date/time of when the entity was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// The date/time of when the entity was last updated.
    /// </summary>
    public required DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Indicates whether the Resource is updateable by clients.
    /// </summary>
    public required bool ReadOnly { get; set; }

    /// <summary>
    /// States that Versions of this Resource adhere to a certain compatibility rule.
    /// </summary>
    public string? Compatibility { get; set; }

    /// <summary>
    /// Information about deprecation status of the entity, if applicable.
    /// </summary>
    public DeprecatedInfo? Deprecated { get; set; }

    /// <summary>
    /// The versionId of the default Version of the Resource.
    /// </summary>
    public required string DefaultVersionId { get; set; }

    /// <summary>
    /// A value of true means that `defaultVersionId` has been explicitly set and MUST NOT automatically change if other Versions are added or removed. A value of false means the default Version MUST be the newest Version, as defined by the Resource's versionmode algorithm.
    /// </summary>
    public required bool DefaultVersionSticky { get; set; }

    /// <summary>
    /// Extension-specific attributes (e.g., `format` and `content_type` for schemas).
    /// </summary>
    public required Dictionary<string, byte[]> Extensions { get; set; }
}
