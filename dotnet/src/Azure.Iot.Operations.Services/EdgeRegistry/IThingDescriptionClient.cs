// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Client for the xRegistry Thing Description extension: create, retrieve, list, and delete Thing
/// Description Versions. Thing Description Versions use integer (<see cref="ulong"/>) version
/// identifiers assigned by the service. The parent Thing Description (Resource) and Thing Description
/// Group are implicitly created when a Version is created.
/// </summary>
/// <remarks>
/// <para>
/// The Thing Description extension is the core xRegistry model specialized for WoT Thing
/// Descriptions; the generic concepts map directly (see <see cref="ICoreClient"/>):
/// </para>
/// <list type="bullet">
/// <item><description>Thing Description Group = xRegistry Group (<c>thingdescriptiongroups</c>).</description></item>
/// <item><description>Thing Description = xRegistry Resource — the named Thing Description that owns its revisions.</description></item>
/// <item><description>
/// Thing Description Version = xRegistry Version — one immutable revision of a Thing Description
/// document. <see cref="Models.ThingDescriptionVersion"/> carries the document bytes plus the
/// revision's metadata; its <c>versionId</c> is a service-assigned integer, not a semantic version.
/// </description></item>
/// </list>
/// </remarks>
public interface IThingDescriptionClient
{
    /// <summary>Creates a Thing Description Version under the given Thing Description, implicitly creating the parent Thing Description if needed.</summary>
    /// <param name="groupId">The Thing Description Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="thingDescriptionId">The Thing Description (Resource) identifier.</param>
    /// <param name="thingDescriptionLabels">Labels applied to the parent Thing Description.</param>
    /// <param name="version">The attributes of the Thing Description Version to create.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the created <see cref="Models.ThingDescriptionVersion"/>.</returns>
    Task<Models.ThingDescriptionVersion> CreateThingDescriptionVersionAsync(GroupId groupId, string thingDescriptionId, IReadOnlyList<Models.Label> thingDescriptionLabels, Models.ThingDescriptionVersionAttributes version, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a Thing Description Version.</summary>
    /// <param name="groupId">The Thing Description Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="thingDescriptionId">The Thing Description (Resource) identifier.</param>
    /// <param name="versionId">The Version to retrieve. Use <see cref="GetThingDescriptionVersionId.ResourceDefault"/> for the Thing Description's default (latest) Version.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the requested <see cref="Models.ThingDescriptionVersion"/>.</returns>
    Task<Models.ThingDescriptionVersion> GetThingDescriptionVersionAsync(GroupId groupId, string thingDescriptionId, GetThingDescriptionVersionId versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Lists the XIDs of Thing Description Versions matching the query, optionally filtered by Thing Description id and/or a single label.</summary>
    /// <param name="groups">The Groups to search; see <see cref="GroupSelector"/> for the available scopes.</param>
    /// <param name="thingDescriptionId">When set, restricts the results to this Thing Description.</param>
    /// <param name="documentHash">When set, restricts the results to entities whose document has this hash.</param>
    /// <param name="label">When set, restricts the results to entities carrying this label.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the XIDs of the matching Thing Description Versions.</returns>
    Task<IReadOnlyList<Models.ThingDescriptionVersionXid>> ListThingDescriptionVersionsAsync(GroupSelector groups, string? thingDescriptionId = null, string? documentHash = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a specific Thing Description Version.</summary>
    /// <param name="groupId">The Thing Description Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="thingDescriptionId">The Thing Description (Resource) identifier.</param>
    /// <param name="versionId">The identifier of the Thing Description Version to delete.</param>
    /// <param name="options">The <see cref="Models.DeleteOptions"/> that control the behavior of the delete operation; when <see langword="null"/>, <see cref="Models.DeleteOptions.Default"/> is used.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the Thing Description Version has been deleted.</returns>
    Task DeleteThingDescriptionVersionAsync(GroupId groupId, string thingDescriptionId, ulong versionId, Models.DeleteOptions? options = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
