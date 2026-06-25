// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Client for the generic core xRegistry CRUD and query API over Groups, Resources, and
/// Versions identified by XID path components. The extension clients (Schema, Thing Description,
/// Thing Model) delegate to this surface for the group/resource/version operations that have no
/// dedicated extension action.
/// </summary>
public interface ICoreClient : IAsyncDisposable
{
    // ---- Group APIs ----

    /// <summary>Lists the IDs of all Groups of the given <paramref name="groupType"/>.</summary>
    /// <param name="groupType">The Group type (the xRegistry Group collection name).</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the IDs of all Groups of the given <paramref name="groupType"/>.</returns>
    Task<IReadOnlyList<string>> ListGroupsAsync(string groupType, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a Group.</summary>
    /// <param name="groupType">The Group type (the xRegistry Group collection name).</param>
    /// <param name="groupId">The Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the requested <see cref="Models.GroupEntity"/>.</returns>
    Task<Models.GroupEntity> GetGroupAsync(string groupType, GroupId groupId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Creates a Group.</summary>
    /// <param name="groupType">The Group type (the xRegistry Group collection name).</param>
    /// <param name="groupId">The Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="attributes">The attributes of the Group to create.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the created <see cref="Models.GroupEntity"/>.</returns>
    Task<Models.GroupEntity> CreateGroupAsync(string groupType, GroupId groupId, Models.GroupAttributes attributes, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a Group. Deletes cascade to all contained Resources and their Versions.</summary>
    /// <param name="groupType">The Group type (the xRegistry Group collection name).</param>
    /// <param name="groupId">The Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the Group has been deleted.</returns>
    Task DeleteGroupAsync(string groupType, GroupId groupId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    // ---- Resource APIs ----

    /// <summary>Retrieves a Resource.</summary>
    /// <param name="groupType">The Group type (the xRegistry Group collection name).</param>
    /// <param name="groupId">The owning Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="resourceType">The Resource type (the xRegistry Resource collection name).</param>
    /// <param name="resourceId">The Resource identifier.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the requested <see cref="Models.ResourceEntity"/>.</returns>
    Task<Models.ResourceEntity> GetResourceAsync(string groupType, GroupId groupId, string resourceType, string resourceId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Creates a Resource together with its default Version.</summary>
    /// <param name="groupType">The Group type (the xRegistry Group collection name).</param>
    /// <param name="groupId">The owning Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="resourceType">The Resource type (the xRegistry Resource collection name).</param>
    /// <param name="resourceId">The Resource identifier.</param>
    /// <param name="meta">The Resource-level metadata attributes.</param>
    /// <param name="resourceExtensions">Resource-level extension attributes, keyed by extension name.</param>
    /// <param name="defaultVersionId">The Version id to assign to the Resource's default Version. Use <see cref="CreateVersionId.ServerAssigned"/> to let the service choose.</param>
    /// <param name="defaultVersion">The attributes of the Resource's default Version.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the created <see cref="Models.ResourceEntity"/>.</returns>
    Task<Models.ResourceEntity> CreateResourceAsync(string groupType, GroupId groupId, string resourceType, string resourceId, Models.ResourceMetaAttributes meta, Dictionary<string, byte[]> resourceExtensions, CreateVersionId defaultVersionId, Models.VersionAttributes defaultVersion, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Lists the XIDs of Resources matching the query, optionally filtered by Resource type and/or a single label.</summary>
    /// <param name="groups">The Groups to search; see <see cref="GroupQuery"/> for the available scopes.</param>
    /// <param name="resourceType">When set, restricts the results to this Resource type.</param>
    /// <param name="label">When set, restricts the results to entities carrying this label.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the XIDs of the matching Resources.</returns>
    Task<IReadOnlyList<Models.ResourceXId>> ListResourcesAsync(GroupQuery groups, string? resourceType = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a Resource. Deletes cascade to all of its Versions.</summary>
    /// <param name="groupType">The Group type (the xRegistry Group collection name).</param>
    /// <param name="groupId">The owning Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="resourceType">The Resource type (the xRegistry Resource collection name).</param>
    /// <param name="resourceId">The Resource identifier.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the Resource has been deleted.</returns>
    Task DeleteResourceAsync(string groupType, GroupId groupId, string resourceType, string resourceId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    // ---- Version APIs ----

    /// <summary>Retrieves a Version. Pass <see cref="GetVersionId.ResourceDefault"/> for the Resource's default (latest) Version.</summary>
    /// <param name="groupType">The Group type (the xRegistry Group collection name).</param>
    /// <param name="groupId">The owning Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="resourceType">The Resource type (the xRegistry Resource collection name).</param>
    /// <param name="resourceId">The owning Resource identifier.</param>
    /// <param name="versionId">The Version to retrieve. Use <see cref="GetVersionId.ResourceDefault"/> for the Resource's default (latest) Version.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the requested <see cref="Models.VersionEntity"/>.</returns>
    Task<Models.VersionEntity> GetVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, GetVersionId versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Creates a Version under a Resource, implicitly creating the parent Resource if needed. Idempotent per the create-or-match-latest contract.</summary>
    /// <param name="groupType">The Group type (the xRegistry Group collection name).</param>
    /// <param name="groupId">The owning Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="resourceType">The Resource type (the xRegistry Resource collection name).</param>
    /// <param name="resourceId">The owning Resource identifier.</param>
    /// <param name="resourceLabels">Labels applied to the parent Resource when it is implicitly created.</param>
    /// <param name="versionId">The Version id to assign. Use <see cref="CreateVersionId.ServerAssigned"/> to let the service choose.</param>
    /// <param name="version">The attributes of the Version to create.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the created <see cref="Models.VersionEntity"/>.</returns>
    Task<Models.VersionEntity> CreateVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, IReadOnlyList<Models.Label> resourceLabels, CreateVersionId versionId, Models.VersionAttributes version, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Lists the XIDs of Versions matching the query, optionally filtered by Resource type, Resource id, and/or a single label.</summary>
    /// <param name="groups">The Groups to search; see <see cref="GroupQuery"/> for the available scopes.</param>
    /// <param name="resourceType">When set, restricts the results to this Resource type.</param>
    /// <param name="resourceId">When set, restricts the results to this Resource.</param>
    /// <param name="label">When set, restricts the results to entities carrying this label.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the XIDs of the matching Versions.</returns>
    Task<IReadOnlyList<Models.VersionXId>> ListVersionsAsync(GroupQuery groups, string? resourceType = null, string? resourceId = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a specific Version of a Resource.</summary>
    /// <param name="groupType">The Group type (the xRegistry Group collection name).</param>
    /// <param name="groupId">The owning Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="resourceType">The Resource type (the xRegistry Resource collection name).</param>
    /// <param name="resourceId">The owning Resource identifier.</param>
    /// <param name="versionId">The identifier of the Version to delete.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the Version has been deleted.</returns>
    Task DeleteVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, string versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
