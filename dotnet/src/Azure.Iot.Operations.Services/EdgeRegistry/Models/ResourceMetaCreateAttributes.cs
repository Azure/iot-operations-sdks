// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Metadata attributes for creating a resource.
/// </summary>
public class ResourceMetaCreateAttributes
{
    /// <summary>
    /// Labels for the resource.
    /// </summary>
    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>
    /// Extension-specific attributes.
    /// </summary>
    public Dictionary<string, byte[]> Extensions { get; set; } = new();
}
