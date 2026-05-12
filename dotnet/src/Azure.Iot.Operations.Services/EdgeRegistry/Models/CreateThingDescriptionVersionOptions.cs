// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.EdgeRegistry.Generated;

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Options for creating a thing description version.
/// </summary>
public class CreateThingDescriptionVersionOptions
{
    /// <summary>
    /// The version identifier for this thing description version.
    /// </summary>
    public string VersionId { get; set; } = default!;

    /// <summary>
    /// The raw thing description document content.
    /// </summary>
    public byte[] ThingDescriptionDocument { get; set; } = default!;

    /// <summary>
    /// Thing description format identifier, e.g. <see cref="ThingDescriptionFormat.WotTd11"/>.
    /// </summary>
    public string Format { get; set; } = ThingDescriptionFormat.WotTd11;

    /// <summary>
    /// The versionId of this version's ancestor if it has an ancestor.
    /// </summary>
    public string? Ancestor { get; set; }

    /// <summary>
    /// Content type of the thing description version.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Description of the thing description version.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Documentation URL or text.
    /// </summary>
    public string? Documentation { get; set; }

    /// <summary>
    /// Labels for the thing description version.
    /// </summary>
    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>
    /// Human-readable name.
    /// </summary>
    public string? Name { get; set; }
}
