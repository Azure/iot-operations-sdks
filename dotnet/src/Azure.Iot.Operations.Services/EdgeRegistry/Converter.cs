// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Maps generated xRegistry wire types to the hand-written <c>EdgeRegistry.Models</c> domain
/// types. Extended one entity at a time as the <see cref="CoreClient"/> methods are implemented.
/// </summary>
internal static class Converter
{
    public static Models.Group ToModel(Generated.Group value) => new()
    {
        Id = value.Id,
        Xid = value.Xid,
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

    public static List<Generated.Label> ToGenerated(List<Models.Label> value)
        => value.Select(label => new Generated.Label { Key = label.Key, Value = label.Value }).ToList();
}
