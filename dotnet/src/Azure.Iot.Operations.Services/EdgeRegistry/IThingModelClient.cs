// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Client for the xRegistry Thing Model extension: create, retrieve, list, and delete Thing Model
/// Versions. Thing Model Versions use integer (<see cref="ulong"/>) version identifiers assigned by
/// the service. The parent Thing Model (Resource) and Thing Model Group are implicitly created when a
/// Version is created.
/// </summary>
/// <remarks>
/// <para>
/// The Thing Model extension is the core xRegistry model specialized for WoT Thing Models; the
/// generic concepts map directly (see <see cref="ICoreClient"/>):
/// </para>
/// <list type="bullet">
/// <item><description>Thing Model Group = xRegistry Group (<c>thingmodelgroups</c>).</description></item>
/// <item><description>Thing Model = xRegistry Resource — the named Thing Model that owns its revisions.</description></item>
/// <item><description>
/// Thing Model Version = xRegistry Version — one immutable revision of a Thing Model document.
/// <see cref="Models.ThingModelVersion"/> carries the document bytes plus the revision's metadata;
/// its <c>versionId</c> is a service-assigned integer, not a semantic version.
/// </description></item>
/// </list>
/// </remarks>
public interface IThingModelClient
{
    /// <summary>Creates a Thing Model Version under the given Thing Model, implicitly creating the parent Thing Model if needed.</summary>
    Task<Models.ThingModelVersion> CreateThingModelVersionAsync(GroupId groupId, string thingModelId, IReadOnlyList<Models.Label> thingModelLabels, Models.ThingModelVersionAttributes version, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a Thing Model Version. Pass <see cref="GetThingModelVersionId.ResourceDefault"/> for the Thing Model's default (latest) Version.</summary>
    Task<Models.ThingModelVersion> GetThingModelVersionAsync(GroupId groupId, string thingModelId, GetThingModelVersionId versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Lists the XIDs of Thing Model Versions matching the query, optionally filtered by Thing Model id and/or a single label.</summary>
    Task<IReadOnlyList<Models.ThingModelVersionXid>> ListThingModelVersionsAsync(GroupSelector groups, string? thingModelId = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a specific Thing Model Version.</summary>
    Task DeleteThingModelVersionAsync(GroupId groupId, string thingModelId, ulong versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
