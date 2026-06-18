// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Selects which Groups to query when no Group type is specified: all Groups, or a specific
/// Group identifier. There is no cloud-default Group without a Group type, so that case is not
/// representable here.
/// </summary>
public readonly struct AnyGroupSelection
{
    private enum Kind : byte
    {
        All,
        Specific,
    }

    private readonly Kind _kind;
    private readonly string? _groupId;

    private AnyGroupSelection(Kind kind, string? groupId)
    {
        _kind = kind;
        _groupId = groupId;
    }

    /// <summary>All Groups, across all Group identifiers.</summary>
    public static AnyGroupSelection All => default;

    /// <summary>A specific Group identifier.</summary>
    public static AnyGroupSelection Specific(string groupId) => new(Kind.Specific, groupId);

    /// <summary>Implicitly treats a string as a specific Group identifier.</summary>
    public static implicit operator AnyGroupSelection(string groupId) => new(Kind.Specific, groupId);

    /// <summary>Resolves to the (groupId, allGroups) scope fields.</summary>
    internal (string? GroupId, bool AllGroups) Resolve()
        => _kind == Kind.Specific ? (_groupId, false) : (null, true);
}
