// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Selects which Groups a list query searches: either within a single Group type (via
/// <see cref="WithinGroupType"/>) or across all Group types (via <see cref="AllGroups"/> /
/// <see cref="GroupAcrossAllTypes"/>). Invalid combinations (such as the cloud-default Group
/// without a Group type) are not representable.
/// </summary>
public readonly struct GroupQuery
{
    private readonly string? _groupType;
    private readonly string? _groupId;
    private readonly bool _allGroups;

    private GroupQuery(string? groupType, string? groupId, bool allGroups)
    {
        _groupType = groupType;
        _groupId = groupId;
        _allGroups = allGroups;
    }

    /// <summary>
    /// Query within a single Group type. Defaults to that Group type's cloud-default Group.
    /// </summary>
    public static GroupQuery WithinGroupType(string groupType, GroupSelector groups = default)
    {
        (string? groupId, bool allGroups) = groups.Resolve();
        return new GroupQuery(groupType, groupId, allGroups);
    }

    /// <summary>Query all Groups across all Group types.</summary>
    public static GroupQuery AllGroups() => new(null, null, true);

    /// <summary>Query a specific Group identifier across all Group types.</summary>
    public static GroupQuery GroupAcrossAllTypes(string groupId) => new(null, groupId, false);

    /// <summary>Resolves to the (groupType, groupId, allGroups) request-payload scope fields.</summary>
    internal (string? GroupType, string? GroupId, bool AllGroups) Resolve()
        => (_groupType, _groupId, _allGroups);
}
