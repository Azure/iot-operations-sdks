// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Maps generated xRegistry wire types to the hand-written <c>EdgeRegistry.Models</c> domain
/// types. Extended one entity at a time as the <see cref="EdgeRegistryClient"/> methods are implemented.
/// </summary>
internal static class Converter
{
    public static Models.GroupEntity ToModel(Generated.Group value) => new()
    {
        Id = value.Id,
        XId = value.Xid,
        Epoch = value.Epoch,
        Name = value.Name,
        Description = value.Description,
        Documentation = value.Documentation,
        Icon = value.Icon,
        Labels = ToModel(value.Labels),
        CreatedAt = value.CreatedAt,
        ModifiedAt = value.ModifiedAt,
        Deprecated = value.Deprecated is null ? null : ToModel(value.Deprecated),
        ResourcesCounts = new Dictionary<string, ulong>(value.ResourcesCounts),
        Extensions = new Dictionary<string, byte[]>(value.Extensions),
    };

    public static Models.DeprecatedInfo ToModel(Generated.DeprecatedInfo value) => new()
    {
        Effective = value.Effective,
        Removal = value.Removal,
        Alternative = value.Alternative,
        Documentation = value.Documentation,
    };

    public static List<Models.Label> ToModel(List<Generated.Label> value)
        => value.Select(label => new Models.Label { Key = label.Key, Value = label.Value }).ToList();

    public static Models.ResourceEntity ToModel(Generated.Resource value) => new()
    {
        Id = value.Id,
        XId = value.Xid,
        Meta = ToModel(value.Meta),
        DefaultVersion = ToModel(value.DefaultVersion),
        VersionsCount = value.VersionsCount,
        Extensions = new Dictionary<string, byte[]>(value.Extensions),
    };

    public static Models.ResourceMeta ToModel(Generated.ResourceMeta value) => new()
    {
        Id = value.Id,
        XId = value.Xid,
        XRef = value.Xref,
        Epoch = value.Epoch,
        Labels = ToModel(value.Labels),
        CreatedAt = value.CreatedAt,
        ModifiedAt = value.ModifiedAt,
        ReadOnly = value.ReadOnly,
        Compatibility = value.Compatibility,
        Deprecated = value.Deprecated is null ? null : ToModel(value.Deprecated),
        DefaultVersionId = value.DefaultVersionId,
        DefaultVersionSticky = value.DefaultVersionSticky,
        Extensions = new Dictionary<string, byte[]>(value.Extensions),
    };

    public static Models.VersionEntity ToModel(Generated.Version value) => new()
    {
        ResourceId = value.ResourceId,
        VersionId = value.VersionId,
        XId = value.Xid,
        Epoch = value.Epoch,
        Name = value.Name,
        IsDefault = value.IsDefault,
        Description = value.Description,
        Documentation = value.Documentation,
        Icon = value.Icon,
        Labels = ToModel(value.Labels),
        CreatedAt = value.CreatedAt,
        ModifiedAt = value.ModifiedAt,
        Ancestor = value.Ancestor,
        ContentType = value.ContentType,
        Format = value.Format,
        FormatValidated = value.FormatValidated is null ? null : ToModel(value.FormatValidated),
        CompatibilityValidated = value.CompatibilityValidated is null ? null : ToModel(value.CompatibilityValidated),
        Document = value.Document,
        DocumentHash = value.DocumentHash,
        Extensions = new Dictionary<string, byte[]>(value.Extensions),
    };

    public static Models.ValidationStatus ToModel(Generated.ValidationStatus value) => new()
    {
        Validated = value.Validated,
        Reason = value.Reason,
    };

    public static Models.ResourceXId ToModel(Generated.ResourceXid value) => new()
    {
        GroupType = value.GroupType,
        GroupId = value.GroupId,
        ResourceType = value.ResourceType,
        ResourceId = value.ResourceId,
    };

    public static List<Models.ResourceXId> ToModel(Generated.ResourceXidList value)
        => value.Resources.Select(resource => ToModel(resource)).ToList();

    public static Models.VersionXId ToModel(Generated.VersionXid value) => new()
    {
        GroupType = value.GroupType,
        GroupId = value.GroupId,
        ResourceType = value.ResourceType,
        ResourceId = value.ResourceId,
        VersionId = value.VersionId,
    };

    public static List<Models.VersionXId> ToModel(Generated.VersionXidList value)
        => value.Versions.Select(version => ToModel(version)).ToList();

    public static Models.SchemaVersion ToModel(Generated.SchemaVersion value) => new()
    {
        VersionId = value.VersionId,
        ResourceId = value.ResourceId,
        Xid = value.Xid,
        Epoch = value.Epoch,
        Name = value.Name,
        IsDefault = value.IsDefault,
        Description = value.Description,
        Documentation = value.Documentation,
        Icon = value.Icon,
        Labels = ToModel(value.Labels),
        CreatedAt = value.CreatedAt,
        ModifiedAt = value.ModifiedAt,
        Ancestor = value.Ancestor,
        ContentType = value.ContentType,
        Format = value.Format,
        FormatValidated = value.FormatValidated is null ? null : ToModel(value.FormatValidated),
        CompatibilityValidated = value.CompatibilityValidated is null ? null : ToModel(value.CompatibilityValidated),
        Document = value.Document,
        DocumentHash = value.DocumentHash,
        Extensions = new Dictionary<string, byte[]>(value.Extensions),
    };

    public static Models.SchemaVersionXid ToModel(Generated.SchemaVersionXid value) => new()
    {
        GroupType = value.GroupType,
        GroupId = value.GroupId,
        ResourceType = value.ResourceType,
        ResourceId = value.ResourceId,
        VersionId = value.VersionId,
    };

    public static List<Models.SchemaVersionXid> ToModel(Generated.SchemaVersionXidList value)
        => value.Versions.Select(version => ToModel(version)).ToList();

    public static Models.ThingDescriptionVersion ToModel(Generated.ThingDescriptionVersion value) => new()
    {
        VersionId = value.VersionId,
        ResourceId = value.ResourceId,
        Xid = value.Xid,
        Epoch = value.Epoch,
        Name = value.Name,
        IsDefault = value.IsDefault,
        Description = value.Description,
        Documentation = value.Documentation,
        Icon = value.Icon,
        Labels = ToModel(value.Labels),
        CreatedAt = value.CreatedAt,
        ModifiedAt = value.ModifiedAt,
        Ancestor = value.Ancestor,
        ContentType = value.ContentType,
        Format = value.Format,
        FormatValidated = value.FormatValidated is null ? null : ToModel(value.FormatValidated),
        CompatibilityValidated = value.CompatibilityValidated is null ? null : ToModel(value.CompatibilityValidated),
        Document = value.Document,
        DocumentHash = value.DocumentHash,
        Extensions = new Dictionary<string, byte[]>(value.Extensions),
    };

    public static Models.ThingDescriptionVersionXid ToModel(Generated.ThingDescriptionVersionXid value) => new()
    {
        GroupType = value.GroupType,
        GroupId = value.GroupId,
        ResourceType = value.ResourceType,
        ResourceId = value.ResourceId,
        VersionId = value.VersionId,
    };

    public static List<Models.ThingDescriptionVersionXid> ToModel(Generated.ThingDescriptionVersionXidList value)
        => value.Versions.Select(version => ToModel(version)).ToList();

    public static Generated.GroupAttributes ToGenerated(Models.GroupAttributes value) => new()
    {
        Name = value.Name,
        Description = value.Description,
        Documentation = value.Documentation,
        Icon = value.Icon,
        Labels = ToGenerated(value.Labels),
        Deprecated = value.Deprecated is null ? null : ToGenerated(value.Deprecated),
        Extensions = new Dictionary<string, byte[]>(value.Extensions),
    };

    public static Generated.DeprecatedInfo ToGenerated(Models.DeprecatedInfo value) => new()
    {
        Effective = value.Effective,
        Removal = value.Removal,
        Alternative = value.Alternative,
        Documentation = value.Documentation,
    };

    public static List<Generated.Label> ToGenerated(IReadOnlyList<Models.Label> value)
        => value.Select(label => new Generated.Label { Key = label.Key, Value = label.Value }).ToList();

    public static Generated.Label ToGenerated(Models.Label value)
        => new() { Key = value.Key, Value = value.Value };

    public static Generated.ResourceMetaAttributes ToGenerated(Models.ResourceMetaAttributes value) => new()
    {
        Xref = value.XRef,
        Labels = ToGenerated(value.Labels),
        Compatibility = value.Compatibility,
        Deprecated = value.Deprecated is null ? null : ToGenerated(value.Deprecated),
        Extensions = new Dictionary<string, byte[]>(value.Extensions),
    };

    public static Generated.VersionAttributes ToGenerated(Models.VersionAttributes value) => new()
    {
        Name = value.Name,
        Description = value.Description,
        Documentation = value.Documentation,
        Icon = value.Icon,
        Labels = ToGenerated(value.Labels),
        Ancestor = value.Ancestor,
        ContentType = value.ContentType,
        Format = value.Format,
        Document = value.Document,
        Extensions = new Dictionary<string, byte[]>(value.Extensions),
    };

    public static Generated.CreateSchemaVersionAttributes ToGenerated(Models.SchemaVersionAttributes value, string? groupId, IReadOnlyList<Models.Label> schemaLabels) => new()
    {
        GroupId = groupId,
        Name = value.Name,
        Description = value.Description,
        Documentation = value.Documentation,
        Icon = value.Icon,
        Labels = ToGenerated(value.Labels),
        Ancestor = value.Ancestor,
        ContentType = value.ContentType,
        Format = value.Format.Value,
        Document = value.Document,
        Extensions = new Dictionary<string, byte[]>(value.Extensions),
        SchemaLabels = ToGenerated(schemaLabels),
    };

    public static Generated.CreateThingDescriptionVersionAttributes ToGenerated(Models.ThingDescriptionVersionAttributes value, string? groupId, IReadOnlyList<Models.Label> thingDescriptionLabels) => new()
    {
        GroupId = groupId,
        Name = value.Name,
        Description = value.Description,
        Documentation = value.Documentation,
        Icon = value.Icon,
        Labels = ToGenerated(value.Labels),
        Ancestor = value.Ancestor,
        ContentType = value.ContentType,
        Format = value.Format.Value,
        Document = value.Document,
        Extensions = new Dictionary<string, byte[]>(value.Extensions),
        ThingDescriptionLabels = ToGenerated(thingDescriptionLabels),
    };
}
