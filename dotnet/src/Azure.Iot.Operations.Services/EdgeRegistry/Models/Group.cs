// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// A Group entity — container for related Resources.
/// </summary>
public class Group
{
    /// <summary>
    /// Group identifier.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Full XID path, e.g. /schemagroups/mygroup
    /// </summary>
    public required string Xid { get; set; }

    /// <summary>
    /// A numeric value used to determine whether an entity has been modified.
    /// </summary>
    public required ulong Epoch { get; set; }

    /// <summary>
    /// Human-readable name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// A human-readable summary of the purpose of the entity.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// A URL to additional information about this entity.
    /// </summary>
    public string? Documentation { get; set; }

    /// <summary>
    /// A URL to a graphical icon for the owning entity.
    /// </summary>
    public string? Icon { get; set; }

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
    /// Information about deprecation status of the entity, if applicable.
    /// </summary>
    public DeprecatedInfo? Deprecated { get; set; }

    /// <summary>
    /// Map of the count of each Resource type contained within this Group, keyed by Resource type.
    /// </summary>
    public required Dictionary<string, ulong> ResourcesCounts { get; set; }

    /// <summary>
    /// Extension-specific attributes (e.g., `envelope`, `protocol` for message Groups).
    /// </summary>
    public required Dictionary<string, byte[]> Extensions { get; set; }
}
