// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Identifies which Group a single-entity operation targets: either the cloud-default Group
/// (the configured namespace) or a specific Group identifier.
/// </summary>
public readonly struct GroupId
{
    private GroupId(string? value) => Value = value;

    /// <summary>The default Group (the configured namespace).</summary>
    public static GroupId CloudDefault => default;

    /// <summary>A specific Group identifier.</summary>
    public static GroupId Specific(string groupId) => new(groupId);

    /// <summary>Implicitly treats a string as a specific Group identifier.</summary>
    public static implicit operator GroupId(string groupId) => new(groupId);

    /// <summary>The wire value: <c>null</c> for the cloud default, otherwise the specific id.</summary>
    internal string? Value { get; }
}
