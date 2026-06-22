// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Attributes needed to create a Thing Description Version. The versionId is assigned by the service.
/// </summary>
public class ThingDescriptionVersionAttributes
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
    /// Queryable Key Value pairs to be added to the Version.
    /// </summary>
    public required List<Label> Labels { get; set; }

    /// <summary>
    /// The versionId of this Version's ancestor if it has an ancestor.
    /// </summary>
    public ulong? Ancestor { get; set; }

    /// <summary>
    /// Content type of the Version document.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Format of the Version document.
    /// </summary>
    public required ThingDescriptionFormat Format { get; set; }

    /// <summary>
    /// Document content for the Version.
    /// </summary>
    public required byte[] Document { get; set; }

    /// <summary>
    /// Extension-specific attributes.
    /// </summary>
    public required Dictionary<string, byte[]> Extensions { get; set; }
}
