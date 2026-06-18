// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Selects which Groups a list query searches: either across all Group types, or within a single
/// Group type. Constructed via <see cref="AcrossAllGroupTypes"/> or <see cref="WithinGroupType"/>;
/// invalid combinations (such as the cloud-default Group without a Group type) are not
/// representable.
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

    /// <summary>Query across all Group types, selecting all Groups or a specific Group identifier.</summary>
    public static GroupQuery AcrossAllGroupTypes(AnyGroupSelection groups)
    {
        (string? groupId, bool allGroups) = groups.Resolve();
        return new GroupQuery(null, groupId, allGroups);
    }

    /// <summary>
    /// Query within a single Group type. Defaults to that Group type's cloud-default Group.
    /// </summary>
    public static GroupQuery WithinGroupType(string groupType, GroupSelection groups = default)
    {
        (string? groupId, bool allGroups) = groups.Resolve();
        return new GroupQuery(groupType, groupId, allGroups);
    }

    /// <summary>Resolves to the (groupType, groupId, allGroups) request-payload scope fields.</summary>
    internal (string? GroupType, string? GroupId, bool AllGroups) Resolve()
        => (_groupType, _groupId, _allGroups);
}
