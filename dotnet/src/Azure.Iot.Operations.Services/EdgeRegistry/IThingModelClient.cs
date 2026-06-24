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
    /// <param name="groupId">The Thing Model Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="thingModelId">The Thing Model (Resource) identifier.</param>
    /// <param name="thingModelLabels">Labels applied to the parent Thing Model when it is implicitly created.</param>
    /// <param name="version">The attributes of the Thing Model Version to create.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the created <see cref="Models.ThingModelVersion"/>.</returns>
    Task<Models.ThingModelVersion> CreateThingModelVersionAsync(GroupId groupId, string thingModelId, IReadOnlyList<Models.Label> thingModelLabels, Models.ThingModelVersionAttributes version, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a Thing Model Version. Pass <see cref="GetThingModelVersionId.ResourceDefault"/> for the Thing Model's default (latest) Version.</summary>
    /// <param name="groupId">The Thing Model Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="thingModelId">The Thing Model (Resource) identifier.</param>
    /// <param name="versionId">The Version to retrieve. Use <see cref="GetThingModelVersionId.ResourceDefault"/> for the Thing Model's default (latest) Version.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the requested <see cref="Models.ThingModelVersion"/>.</returns>
    Task<Models.ThingModelVersion> GetThingModelVersionAsync(GroupId groupId, string thingModelId, GetThingModelVersionId versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Lists the XIDs of Thing Model Versions matching the query, optionally filtered by Thing Model id and/or a single label.</summary>
    /// <param name="groups">The Groups to search; see <see cref="GroupSelector"/> for the available scopes.</param>
    /// <param name="thingModelId">When set, restricts the results to this Thing Model.</param>
    /// <param name="label">When set, restricts the results to entities carrying this label.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the XIDs of the matching Thing Model Versions.</returns>
    Task<IReadOnlyList<Models.ThingModelVersionXid>> ListThingModelVersionsAsync(GroupSelector groups, string? thingModelId = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a specific Thing Model Version.</summary>
    /// <param name="groupId">The Thing Model Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="thingModelId">The Thing Model (Resource) identifier.</param>
    /// <param name="versionId">The identifier of the Thing Model Version to delete.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the Thing Model Version has been deleted.</returns>
    Task DeleteThingModelVersionAsync(GroupId groupId, string thingModelId, ulong versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
