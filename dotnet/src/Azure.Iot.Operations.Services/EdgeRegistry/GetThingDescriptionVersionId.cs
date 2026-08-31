// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Selects which Thing Description Version to retrieve: either the Thing Description's default
/// (latest) Version or a specific integer Version identifier.
/// </summary>
public readonly struct GetThingDescriptionVersionId
{
    private GetThingDescriptionVersionId(ulong? value) => Value = value;

    /// <summary>The Thing Description's default (latest) Version.</summary>
    public static GetThingDescriptionVersionId ResourceDefault => default;

    /// <summary>A specific Version identifier.</summary>
    public static GetThingDescriptionVersionId Specific(ulong versionId) => new(versionId);

    /// <summary>Implicitly treats an integer as a specific Version identifier.</summary>
    public static implicit operator GetThingDescriptionVersionId(ulong versionId) => new(versionId);

    /// <summary>The wire value: <c>null</c> for the Thing Description default, otherwise the specific id.</summary>
    internal ulong? Value { get; }
}
