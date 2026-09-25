// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Request payload for creating or updating a Group.
/// </summary>
public class CoreGroupAttributes
{
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
    /// Information about deprecation status of the entity, if applicable.
    /// </summary>
    public DeprecatedInfo? Deprecated { get; set; }

    /// <summary>
    /// Extension-specific attributes (e.g., `envelope`, `protocol` for message Groups).
    /// </summary>
    public required Dictionary<string, byte[]> Extensions { get; set; }
}
