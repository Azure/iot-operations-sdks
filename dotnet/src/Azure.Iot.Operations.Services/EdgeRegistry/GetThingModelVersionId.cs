// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Selects which Thing Model Version to retrieve: either the Thing Model's default (latest) Version
/// or a specific integer Version identifier.
/// </summary>
public readonly struct GetThingModelVersionId
{
    private GetThingModelVersionId(ulong? value) => Value = value;

    /// <summary>The Thing Model's default (latest) Version.</summary>
    public static GetThingModelVersionId ResourceDefault => default;

    /// <summary>A specific Version identifier.</summary>
    public static GetThingModelVersionId Specific(ulong versionId) => new(versionId);

    /// <summary>Implicitly treats an integer as a specific Version identifier.</summary>
    public static implicit operator GetThingModelVersionId(ulong versionId) => new(versionId);

    /// <summary>The wire value: <c>null</c> for the Thing Model default, otherwise the specific id.</summary>
    internal ulong? Value { get; }
}
