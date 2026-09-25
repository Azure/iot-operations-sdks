// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Selects which Schema Version to retrieve: either the Schema's default (latest) Version or a
/// specific integer Version identifier.
/// </summary>
public readonly struct GetSchemaVersionId
{
    private GetSchemaVersionId(ulong? value) => Value = value;

    /// <summary>The Schema's default (latest) Version.</summary>
    public static GetSchemaVersionId ResourceDefault => default;

    /// <summary>A specific Version identifier.</summary>
    public static GetSchemaVersionId Specific(ulong versionId) => new(versionId);

    /// <summary>Implicitly treats an integer as a specific Version identifier.</summary>
    public static implicit operator GetSchemaVersionId(ulong versionId) => new(versionId);

    /// <summary>The wire value: <c>null</c> for the Schema default, otherwise the specific id.</summary>
    internal ulong? Value { get; }
}
