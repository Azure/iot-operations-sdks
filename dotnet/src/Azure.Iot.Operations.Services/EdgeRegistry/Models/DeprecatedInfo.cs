// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Information about the deprecation status of an entity.
/// </summary>
public class DeprecatedInfo
{
    /// <summary>
    /// Indicates the time when the entity entered, or will enter, a deprecated state. The date MAY be in the past or future. If this property is not present the entity is already in a deprecated state.
    /// </summary>
    public DateTime? Effective { get; set; }

    /// <summary>
    /// Indicates the time when the entity will be removed. The entity MUST NOT be removed before this time. If this property is not present, the client cannot make any assumptions as to when the entity might be removed. This MUST NOT be sooner than the `effective` time, if that is present.
    /// </summary>
    public DateTime? Removal { get; set; }

    /// <summary>
    /// The URL to an alternative entity the client can consider as a replacement for this entity. There is no guarantee that the referenced entity is an exact replacement.
    /// </summary>
    public string? Alternative { get; set; }

    /// <summary>
    /// The URL to additional information about the deprecation of this entity.
    /// </summary>
    public string? Documentation { get; set; }
}
