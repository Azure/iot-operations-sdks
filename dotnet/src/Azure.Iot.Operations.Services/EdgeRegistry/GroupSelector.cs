// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Selects which Groups an operation targets within a known Group type: the cloud-default Group
/// (the configured namespace), all Groups, or a specific Group identifier. Pairs with
/// <see cref="GroupQuery.WithinGroupType"/>; the cloud-default Group is only meaningful within a
/// Group type, so querying across all Group types uses dedicated <see cref="GroupQuery"/> factories
/// instead.
/// </summary>
public readonly struct GroupSelector
{
    private enum Kind : byte
    {
        Default,
        All,
        Specific,
    }

    private readonly Kind _kind;
    private readonly string? _groupId;

    private GroupSelector(Kind kind, string? groupId)
    {
        _kind = kind;
        _groupId = groupId;
    }

    /// <summary>The cloud-default Group (the configured namespace) of the Group type.</summary>
    public static GroupSelector Default => default;

    /// <summary>All Groups of the Group type.</summary>
    public static GroupSelector All => new(Kind.All, null);

    /// <summary>A specific Group identifier.</summary>
    public static GroupSelector Specific(string groupId) => new(Kind.Specific, groupId);

    /// <summary>Implicitly treats a string as a specific Group identifier.</summary>
    public static implicit operator GroupSelector(string groupId) => new(Kind.Specific, groupId);

    /// <summary>Resolves to the (groupId, allGroups) scope fields.</summary>
    internal (string? GroupId, bool AllGroups) Resolve() => _kind switch
    {
        Kind.All => (null, true),
        Kind.Specific => (_groupId, false),
        _ => (null, false),
    };
}
