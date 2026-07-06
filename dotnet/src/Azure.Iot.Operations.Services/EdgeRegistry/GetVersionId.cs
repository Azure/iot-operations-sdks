// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Selects which Version to retrieve: either the Resource's default (latest) Version or a
/// specific Version identifier.
/// </summary>
public readonly struct GetVersionId
{
    private GetVersionId(string? value) => Value = value;

    /// <summary>The Resource's default (latest) Version.</summary>
    public static GetVersionId ResourceDefault => default;

    /// <summary>A specific Version identifier.</summary>
    public static GetVersionId Specific(string versionId) => new(versionId);

    /// <summary>Implicitly treats a string as a specific Version identifier.</summary>
    public static implicit operator GetVersionId(string versionId) => new(versionId);

    /// <summary>The wire value: <c>null</c> for the resource default, otherwise the specific id.</summary>
    internal string? Value { get; }
}
