// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// The XID components that identify a Thing Model Version.
/// </summary>
public class ThingModelVersionXid
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
    /// Resource (Thing Model) identifier.
    /// </summary>
    public required string ResourceId { get; set; }

    /// <summary>
    /// Version identifier.
    /// </summary>
    public required ulong VersionId { get; set; }
}
