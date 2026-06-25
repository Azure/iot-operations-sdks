// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Mutable attributes for creating or updating a Resource (its `meta` sub-entity).
/// </summary>
public class ResourceMetaAttributes
{
    /// <summary>
    /// Indicates that this Resource is a reference to another Resource within the same Registry. The XID path of the referenced Resource.
    /// </summary>
    public string? XRef { get; set; }

    /// <summary>
    /// A mechanism in which additional metadata about the entity can be stored without changing the model definition of the entity. Labels can be used for querying.
    /// </summary>
    public required List<Label> Labels { get; set; }

    /// <summary>
    /// States that Versions of this Resource adhere to a certain compatibility rule.
    /// </summary>
    public string? Compatibility { get; set; }

    /// <summary>
    /// Information about deprecation status of the entity, if applicable.
    /// </summary>
    public DeprecatedInfo? Deprecated { get; set; }

    /// <summary>
    /// Extension-specific attributes (e.g., `format` and `content_type` for schemas).
    /// </summary>
    public required Dictionary<string, byte[]> Extensions { get; set; }
}
