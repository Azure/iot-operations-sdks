// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// <see cref="EdgeRegistryClient"/> implementation of the core xRegistry surface
/// (<see cref="ICoreClient"/>): Group, Resource, and Version APIs.
/// </summary>
public sealed partial class EdgeRegistryClient : ICoreClient
{
    // ---- Group APIs ----

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListGroupsAsync(string groupType, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var output = await _coreStub.ListGroupsAsync(
            additionalTopicTokenMap: GroupTopicTokens(groupType),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return output.Ids;
    }

    /// <inheritdoc/>
    public async Task<Models.CoreGroupEntity> GetGroupAsync(string groupType, GroupId groupId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.GetGroupInputArguments request = new() { GroupId = groupId.Value };

        var output = await _coreStub.GetGroupAsync(
            request,
            additionalTopicTokenMap: GroupTopicTokens(groupType),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<Models.CoreGroupEntity> CreateGroupAsync(string groupType, GroupId groupId, Models.CoreGroupAttributes attributes, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.GroupAttributes request = Converter.ToGenerated(attributes);
        request.GroupId = groupId.Value;

        var output = await _coreStub.CreateGroupAsync(
            request,
            additionalTopicTokenMap: GroupTopicTokens(groupType),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task DeleteGroupAsync(string groupType, GroupId groupId, Models.DeleteOptions? options = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        DeleteGroupInputArguments request = new() { GroupId = groupId.Value, Options = Converter.ToGenerated(options ?? Models.DeleteOptions.Default) };

        await _coreStub.DeleteGroupAsync(
            request,
            additionalTopicTokenMap: GroupTopicTokens(groupType),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);
    }

    // ---- Resource APIs ----

    /// <inheritdoc/>
    public async Task<Models.CoreResourceEntity> GetResourceAsync(string groupType, GroupId groupId, string resourceType, string resourceId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.GetResourceInputArguments request = new() { GroupId = groupId.Value };

        var output = await _coreStub.GetResourceAsync(
            request,
            additionalTopicTokenMap: ResourceTopicTokens(groupType, resourceType, resourceId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<Models.CoreResourceEntity> CreateResourceAsync(string groupType, GroupId groupId, string resourceType, string resourceId, Models.CoreResourceMetaAttributes meta, Dictionary<string, byte[]> resourceExtensions, CreateVersionId defaultVersionId, Models.CoreVersionAttributes defaultVersion, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.CreateResourceRequestPayload request = new()
        {
            GroupId = groupId.Value,
            Meta = Converter.ToGenerated(meta),
            DefaultVersion = Converter.ToGenerated(defaultVersion),
            DefaultVersionId = defaultVersionId.Value,
            Extensions = new Dictionary<string, byte[]>(resourceExtensions),
        };

        var output = await _coreStub.CreateResourceAsync(
            request,
            additionalTopicTokenMap: ResourceTopicTokens(groupType, resourceType, resourceId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Models.ResourceXId>> ListResourcesAsync(GroupQuery groups, string? resourceType = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        (string? groupType, string? groupId, bool allGroups) = groups.Resolve();
        Generated.ListResourcesRequestPayload request = new()
        {
            GroupType = groupType,
            GroupId = groupId,
            AllGroups = allGroups,
            ResourceType = resourceType,
            Label = label is null ? null : Converter.ToGenerated(label),
        };

        var output = await _coreStub.ListResourcesAsync(
            request,
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task DeleteResourceAsync(string groupType, GroupId groupId, string resourceType, string resourceId, Models.DeleteOptions? options = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        DeleteResourceInputArguments request = new() { GroupId = groupId.Value, Options = Converter.ToGenerated(options ?? Models.DeleteOptions.Default) };

        await _coreStub.DeleteResourceAsync(
            request,
            additionalTopicTokenMap: ResourceTopicTokens(groupType, resourceType, resourceId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);
    }

    // ---- Version APIs ----

    /// <inheritdoc/>
    public async Task<Models.CoreVersionEntity> GetVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, GetVersionId versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.GetVersionInputArguments request = new()
        {
            GroupId = groupId.Value,
            VersionId = versionId.Value,
        };

        var output = await _coreStub.GetVersionAsync(
            request,
            additionalTopicTokenMap: ResourceTopicTokens(groupType, resourceType, resourceId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<Models.CoreVersionEntity> CreateVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, IReadOnlyList<Models.Label> resourceLabels, CreateVersionId versionId, Models.CoreVersionAttributes version, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.CreateVersionRequestPayload request = new()
        {
            GroupId = groupId.Value,
            VersionId = versionId.Value,
            Version = Converter.ToGenerated(version),
            ResourceLabels = Converter.ToGenerated(resourceLabels),
        };

        var output = await _coreStub.CreateVersionAsync(
            request,
            additionalTopicTokenMap: ResourceTopicTokens(groupType, resourceType, resourceId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Models.VersionXId>> ListVersionsAsync(GroupQuery groups, string? resourceType = null, string? resourceId = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        (string? groupType, string? groupId, bool allGroups) = groups.Resolve();
        Generated.ListVersionsRequestPayload request = new()
        {
            GroupType = groupType,
            GroupId = groupId,
            AllGroups = allGroups,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Label = label is null ? null : Converter.ToGenerated(label),
        };

        var output = await _coreStub.ListVersionsAsync(
            request,
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task DeleteVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, string versionId, Models.DeleteOptions? options = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        DeleteVersionInputArguments request = new() { GroupId = groupId.Value, Options = Converter.ToGenerated(options ?? Models.DeleteOptions.Default) };

        await _coreStub.DeleteVersionAsync(
            request,
            additionalTopicTokenMap: VersionTopicTokens(groupType, resourceType, resourceId, versionId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);
    }
}
