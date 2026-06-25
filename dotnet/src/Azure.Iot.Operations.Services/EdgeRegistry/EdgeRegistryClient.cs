// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Default <see cref="IEdgeRegistryClient"/> implementation. Wraps the generated core xRegistry RPC
/// client (CoreClientStub), routing XID components into per-call topic tokens and mapping the
/// generated wire types to the EdgeRegistry.Models domain types.
/// </summary>
public sealed class EdgeRegistryClient : IEdgeRegistryClient
{
    private static readonly TimeSpan s_defaultCommandTimeout = TimeSpan.FromSeconds(10);

    private readonly CoreClientStub _stub;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="EdgeRegistryClient"/> class.
    /// </summary>
    /// <param name="applicationContext">The shared application context.</param>
    /// <param name="mqttClient">The MQTT client used for RPC.</param>
    public EdgeRegistryClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
    {
        _stub = new CoreClientStub(applicationContext, mqttClient);
    }

    // ---- Group APIs ----

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListGroupsAsync(string groupType, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var output = await _stub.ListGroupsAsync(
            additionalTopicTokenMap: GroupTopicTokens(groupType),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return output.Ids;
    }

    /// <inheritdoc/>
    public async Task<Models.GroupEntity> GetGroupAsync(string groupType, GroupId groupId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.GetGroupInputArguments request = new() { GroupId = groupId.Value };

        var output = await _stub.GetGroupAsync(
            request,
            additionalTopicTokenMap: GroupTopicTokens(groupType),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<Models.GroupEntity> CreateGroupAsync(string groupType, GroupId groupId, Models.GroupAttributes attributes, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.GroupAttributes request = Converter.ToGenerated(attributes);
        request.GroupId = groupId.Value;

        var output = await _stub.CreateGroupAsync(
            request,
            additionalTopicTokenMap: GroupTopicTokens(groupType),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task DeleteGroupAsync(string groupType, GroupId groupId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.DeleteGroupInputArguments request = new() { GroupId = groupId.Value };

        await _stub.DeleteGroupAsync(
            request,
            additionalTopicTokenMap: GroupTopicTokens(groupType),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);
    }

    // ---- Resource APIs ----

    /// <inheritdoc/>
    public async Task<Models.ResourceEntity> GetResourceAsync(string groupType, GroupId groupId, string resourceType, string resourceId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.GetResourceInputArguments request = new() { GroupId = groupId.Value };

        var output = await _stub.GetResourceAsync(
            request,
            additionalTopicTokenMap: ResourceTopicTokens(groupType, resourceType, resourceId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<Models.ResourceEntity> CreateResourceAsync(string groupType, GroupId groupId, string resourceType, string resourceId, Models.ResourceMetaAttributes meta, Dictionary<string, byte[]> resourceExtensions, CreateVersionId defaultVersionId, Models.VersionAttributes defaultVersion, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
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

        var output = await _stub.CreateResourceAsync(
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

        var output = await _stub.ListResourcesAsync(
            request,
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task DeleteResourceAsync(string groupType, GroupId groupId, string resourceType, string resourceId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.DeleteResourceInputArguments request = new() { GroupId = groupId.Value };

        await _stub.DeleteResourceAsync(
            request,
            additionalTopicTokenMap: ResourceTopicTokens(groupType, resourceType, resourceId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);
    }

    // ---- Version APIs ----

    /// <inheritdoc/>
    public async Task<Models.VersionEntity> GetVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, GetVersionId versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.GetVersionInputArguments request = new()
        {
            GroupId = groupId.Value,
            VersionId = versionId.Value,
        };

        var output = await _stub.GetVersionAsync(
            request,
            additionalTopicTokenMap: ResourceTopicTokens(groupType, resourceType, resourceId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<Models.VersionEntity> CreateVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, IReadOnlyList<Models.Label> resourceLabels, CreateVersionId versionId, Models.VersionAttributes version, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
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

        var output = await _stub.CreateVersionAsync(
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

        var output = await _stub.ListVersionsAsync(
            request,
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task DeleteVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, string versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.DeleteVersionInputArguments request = new() { GroupId = groupId.Value };

        await _stub.DeleteVersionAsync(
            request,
            additionalTopicTokenMap: VersionTopicTokens(groupType, resourceType, resourceId, versionId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);
    }

    // ---- Topic token helpers ----

    /// <summary>Builds the topic tokens for a Group-scoped request.</summary>
    private static Dictionary<string, string> GroupTopicTokens(string groupType)
        => new() { ["groupType"] = groupType };

    /// <summary>Builds the topic tokens for a Resource-scoped request.</summary>
    private static Dictionary<string, string> ResourceTopicTokens(string groupType, string resourceType, string resourceId)
        => new()
        {
            ["groupType"] = groupType,
            ["resourceType"] = resourceType,
            ["resourceId"] = resourceId,
        };

    /// <summary>Builds the topic tokens for a Version-scoped request that carries the Version id in the topic.</summary>
    private static Dictionary<string, string> VersionTopicTokens(string groupType, string resourceType, string resourceId, string versionId)
    {
        Dictionary<string, string> tokens = ResourceTopicTokens(groupType, resourceType, resourceId);
        tokens["versionId"] = versionId;
        return tokens;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        await _stub.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _stub.DisposeAsync().ConfigureAwait(false);
    }
}
