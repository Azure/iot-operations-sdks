// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Identifies the Version id to assign when creating a Version: either server-assigned (the
/// service chooses the next identifier) or a specific identifier.
/// </summary>
public readonly struct CreateVersionId
{
    private CreateVersionId(string? value) => Value = value;

    /// <summary>The service assigns the Version identifier.</summary>
    public static CreateVersionId ServerAssigned => default;

    /// <summary>A specific Version identifier.</summary>
    public static CreateVersionId Specific(string versionId) => new(versionId);

    /// <summary>Implicitly treats a string as a specific Version identifier.</summary>
    public static implicit operator CreateVersionId(string versionId) => new(versionId);

    /// <summary>The wire value: <c>null</c> for server-assigned, otherwise the specific id.</summary>
    internal string? Value { get; }
}
