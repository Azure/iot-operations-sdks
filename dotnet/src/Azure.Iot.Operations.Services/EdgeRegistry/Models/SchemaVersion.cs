// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// A specific Version of a Schema. Self-contained: combines the generic Version fields and the
/// schema-specific fields, with integer-typed <see cref="VersionId"/> and <see cref="Ancestor"/>.
/// </summary>
public class SchemaVersion
{
    /// <summary>
    /// Version identifier.
    /// </summary>
    public required ulong VersionId { get; set; }

    /// <summary>
    /// Schema (Resource) identifier.
    /// </summary>
    public required string ResourceId { get; set; }

    /// <summary>
    /// Full XID path.
    /// </summary>
    public required string Xid { get; set; }

    /// <summary>
    /// A numeric value used to determine whether an entity has been modified.
    /// </summary>
    public required ulong Epoch { get; set; }

    /// <summary>
    /// Human-readable name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Indicates whether this Version is the default Version of the owning Schema.
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
    public required ulong Ancestor { get; set; }

    /// <summary>
    /// The media type of the entity as defined by RFC9110.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Identifies the format of the schema document.
    /// </summary>
    public required SchemaFormat Format { get; set; }

    /// <summary>
    /// When format validation is enabled, indicates whether the server has validated that the Version conforms to the rules defined by its <see cref="Format"/>.
    /// </summary>
    public ValidationStatus? FormatValidated { get; set; }

    /// <summary>
    /// When compatibility validation is enabled, indicates whether the server has validated that the Version conforms to the rules defined by its Schema's compatibility attribute.
    /// </summary>
    public ValidationStatus? CompatibilityValidated { get; set; }

    /// <summary>
    /// The raw schema document for this Version.
    /// </summary>
    public required byte[] Document { get; set; }

    /// <summary>
    /// The hash of the schema document for this Version.
    /// </summary>
    public required string DocumentHash { get; set; }

    /// <summary>
    /// Extension-specific attributes.
    /// </summary>
    public required Dictionary<string, byte[]> Extensions { get; set; }
}
