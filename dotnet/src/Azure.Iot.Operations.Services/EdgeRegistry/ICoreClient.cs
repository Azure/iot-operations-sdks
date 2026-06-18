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

    /// <summary>Retrieves a Group. Uses the default Group (namespace) when <paramref name="groupId"/> is null.</summary>
    Task<Models.Group> GetGroupAsync(string groupType, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Creates a Group. Uses the default Group (namespace) when <paramref name="groupId"/> is null.</summary>
    Task<Models.Group> CreateGroupAsync(string groupType, Models.GroupAttributes attributes, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a Group. Deletes cascade to all contained Resources and their Versions.</summary>
    Task DeleteGroupAsync(string groupType, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    // ---- Resource APIs ----

    /// <summary>Lists the IDs of all Resources of <paramref name="resourceType"/> within a Group.</summary>
    Task<IReadOnlyList<string>> ListResourcesAsync(string groupType, string resourceType, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Lists the XIDs of Resources carrying the given label, optionally filtered by group/resource type.</summary>
    Task<IReadOnlyList<Models.ResourceXid>> ListResourcesWithLabelAsync(string labelKey, string labelValue, string? groupType = null, string? groupId = null, string? resourceType = null, bool allGroups = false, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a Resource.</summary>
    Task<Models.Resource> GetResourceAsync(string groupType, string resourceType, string resourceId, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Creates a Resource together with its default Version.</summary>
    Task<Models.Resource> CreateResourceAsync(string groupType, string resourceType, string resourceId, Models.ResourceMetaAttributes meta, Models.VersionAttributes defaultVersion, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a Resource. Deletes cascade to all of its Versions.</summary>
    Task DeleteResourceAsync(string groupType, string resourceType, string resourceId, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    // ---- Version APIs ----

    /// <summary>Lists the IDs of all Versions of a Resource.</summary>
    Task<IReadOnlyList<string>> ListVersionsAsync(string groupType, string resourceType, string resourceId, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Lists the XIDs of Versions carrying the given label, optionally filtered by group/resource type and resource.</summary>
    Task<IReadOnlyList<Models.VersionXid>> ListVersionsWithLabelAsync(string labelKey, string labelValue, string? groupType = null, string? groupId = null, string? resourceType = null, string? resourceId = null, bool allGroups = false, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a Version. Returns the default (latest) Version when <paramref name="versionId"/> is null.</summary>
    Task<Models.Version> GetVersionAsync(string groupType, string resourceType, string resourceId, string? versionId = null, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Creates a Version under a Resource, implicitly creating the parent Resource if needed. Idempotent per the create-or-match-latest contract.</summary>
    Task<Models.Version> CreateVersionAsync(string groupType, string resourceType, string resourceId, Models.VersionAttributes version, IReadOnlyList<Models.Label> resourceLabels, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a specific Version of a Resource.</summary>
    Task DeleteVersionAsync(string groupType, string resourceType, string resourceId, string versionId, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
