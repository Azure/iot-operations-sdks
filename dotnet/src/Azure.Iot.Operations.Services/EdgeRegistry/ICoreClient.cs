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
    Task<IReadOnlyList<string>> ListGroupsAsync(string groupType, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a Group.</summary>
    Task<Models.GroupEntity> GetGroupAsync(string groupType, GroupId groupId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Creates a Group.</summary>
    Task<Models.GroupEntity> CreateGroupAsync(string groupType, GroupId groupId, Models.GroupAttributes attributes, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a Group. Deletes cascade to all contained Resources and their Versions.</summary>
    Task DeleteGroupAsync(string groupType, GroupId groupId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    // ---- Resource APIs ----

    /// <summary>Retrieves a Resource.</summary>
    Task<Models.ResourceEntity> GetResourceAsync(string groupType, GroupId groupId, string resourceType, string resourceId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Creates a Resource together with its default Version.</summary>
    Task<Models.ResourceEntity> CreateResourceAsync(string groupType, GroupId groupId, string resourceType, string resourceId, Models.ResourceMetaAttributes meta, Dictionary<string, byte[]> resourceExtensions, CreateVersionId defaultVersionId, Models.VersionAttributes defaultVersion, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Lists the XIDs of Resources matching the query, optionally filtered by Resource type and/or a single label.</summary>
    Task<IReadOnlyList<Models.ResourceXId>> ListResourcesAsync(GroupQuery groups, string? resourceType = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a Resource. Deletes cascade to all of its Versions.</summary>
    Task DeleteResourceAsync(string groupType, GroupId groupId, string resourceType, string resourceId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    // ---- Version APIs ----

    /// <summary>Retrieves a Version. Pass <see cref="GetVersionId.ResourceDefault"/> for the Resource's default (latest) Version.</summary>
    Task<Models.VersionEntity> GetVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, GetVersionId versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Creates a Version under a Resource, implicitly creating the parent Resource if needed. Idempotent per the create-or-match-latest contract.</summary>
    Task<Models.VersionEntity> CreateVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, IReadOnlyList<Models.Label> resourceLabels, CreateVersionId versionId, Models.VersionAttributes version, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Lists the XIDs of Versions matching the query, optionally filtered by Resource type, Resource id, and/or a single label.</summary>
    Task<IReadOnlyList<Models.VersionXId>> ListVersionsAsync(GroupQuery groups, string? resourceType = null, string? resourceId = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a specific Version of a Resource.</summary>
    Task DeleteVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, string versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
