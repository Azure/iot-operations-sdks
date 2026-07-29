// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Attributes needed to create a Version.
/// </summary>
public class CoreVersionAttributes
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
    public string? Ancestor { get; set; }

    /// <summary>
    /// Content type of the Version document.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Format identifier of the Version document (resource-type-specific, e.g. `JsonSchema/draft-07`, `JSON-LD/1.1`).
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Base64-encoded document content for the Version. The interpretation (schema, thing description, thing model, etc.) is determined by the Resource type.
    /// </summary>
    public byte[]? Document { get; set; }

    /// <summary>
    /// Extension-specific attributes.
    /// </summary>
    public required Dictionary<string, byte[]> Extensions { get; set; }
}
