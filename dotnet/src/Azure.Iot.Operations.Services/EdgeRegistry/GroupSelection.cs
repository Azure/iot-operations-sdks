// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Selects which Groups to query within a known Group type: all Groups, the cloud-default Group
/// (the configured namespace), or a specific Group identifier.
/// </summary>
public readonly struct GroupSelection
{
    private enum Kind : byte
    {
        Default,
        All,
        Specific,
    }

    private readonly Kind _kind;
    private readonly string? _groupId;

    private GroupSelection(Kind kind, string? groupId)
    {
        _kind = kind;
        _groupId = groupId;
    }

    /// <summary>The cloud-default Group (the configured namespace) of the Group type.</summary>
    public static GroupSelection Default => default;

    /// <summary>All Groups of the Group type.</summary>
    public static GroupSelection All => new(Kind.All, null);

    /// <summary>A specific Group identifier.</summary>
    public static GroupSelection Specific(string groupId) => new(Kind.Specific, groupId);

    /// <summary>Implicitly treats a string as a specific Group identifier.</summary>
    public static implicit operator GroupSelection(string groupId) => new(Kind.Specific, groupId);

    /// <summary>Resolves to the (groupId, allGroups) scope fields.</summary>
    internal (string? GroupId, bool AllGroups) Resolve() => _kind switch
    {
        Kind.All => (null, true),
        Kind.Specific => (_groupId, false),
        _ => (null, false),
    };
}
