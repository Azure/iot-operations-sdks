// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// The XID components that identify a Resource.
/// </summary>
public class ResourceXId
{
    /// <summary>
    /// Group type.
    /// </summary>
    public required string GroupType { get; set; }

    /// <summary>
    /// Group identifier.
    /// </summary>
    public required string GroupId { get; set; }

    /// <summary>
    /// Resource type.
    /// </summary>
    public required string ResourceType { get; set; }

    /// <summary>
    /// Resource identifier.
    /// </summary>
    public required string ResourceId { get; set; }
}
