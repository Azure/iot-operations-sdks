// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// A specific Version of a Resource.
/// </summary>
public class CoreVersionEntity
{
    /// <summary>
    /// Resource identifier.
    /// </summary>
    public required string ResourceId { get; set; }

    /// <summary>
    /// Version identifier.
    /// </summary>
    public required string VersionId { get; set; }

    /// <summary>
    /// Full XID path.
    /// </summary>
    public required string XId { get; set; }

    /// <summary>
    /// A numeric value used to determine whether an entity has been modified.
    /// </summary>
    public required ulong Epoch { get; set; }

    /// <summary>
    /// Human-readable name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Indicates whether this Version is the default Version of the owning Resource.
    /// </summary>
    public required bool IsDefault { get; set; }

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
    /// The date/time of when the entity was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// The date/time of when the entity was last updated.
    /// </summary>
    public required DateTime ModifiedAt { get; set; }

    /// <summary>
    /// The versionId of this Version's ancestor, or this Version's versionId if it has no ancestor.
    /// </summary>
    public required string Ancestor { get; set; }

    /// <summary>
    /// The media type of the entity as defined by RFC9110.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Identifies what the Version represents (e.g. `JsonSchema/draft-07`, `JSON-LD/1.1`).
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// When format validation is enabled, indicates whether the server has validated that the Version conforms to the rules defined by its `format` attribute.
    /// </summary>
    public ValidationStatus? FormatValidated { get; set; }

    /// <summary>
    /// When compatibility validation is enabled, indicates whether the server has validated that the Version conforms to the rules defined by its Resource's `meta.compatibility` attribute.
    /// </summary>
    public ValidationStatus? CompatibilityValidated { get; set; }

    /// <summary>
    /// The raw document content for this Version as base64-encoded bytes. The interpretation (schema, thing description, thing model, …) is determined by the parent Resource's type.
    /// </summary>
    public byte[]? Document { get; set; }

    /// <summary>
    /// The hash of the document content for this Version.
    /// </summary>
    public string? DocumentHash { get; set; }

    /// <summary>
    /// Extension-specific attributes.
    /// </summary>
    public required Dictionary<string, byte[]> Extensions { get; set; }
}
