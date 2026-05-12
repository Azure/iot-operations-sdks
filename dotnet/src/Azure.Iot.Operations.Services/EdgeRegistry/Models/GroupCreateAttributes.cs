// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Attributes for creating or updating a group.
/// </summary>
public class GroupCreateAttributes
{
    /// <summary>
    /// Human-readable name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Description of the group.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Documentation URL or text.
    /// </summary>
    public string? Documentation { get; set; }

    /// <summary>
    /// Labels for the group.
    /// </summary>
    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>
    /// Extension-specific attributes.
    /// </summary>
    public Dictionary<string, byte[]> Extensions { get; set; } = new();
}
