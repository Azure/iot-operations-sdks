// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Default <see cref="ICoreClient"/> implementation. Wraps the generated core xRegistry RPC
/// client (CoreClientStub), routing XID components into per-call topic tokens and mapping the
/// generated wire types to the EdgeRegistry.Models domain types.
/// </summary>
public sealed class CoreClient : ICoreClient
{
    private static readonly TimeSpan s_defaultCommandTimeout = TimeSpan.FromSeconds(10);

    private readonly CoreClientStub _stub;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoreClient"/> class.
    /// </summary>
    /// <param name="applicationContext">The shared application context.</param>
    /// <param name="mqttClient">The MQTT client used for RPC.</param>
    public CoreClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
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
            requestMetadata: null,
            additionalTopicTokenMap: GroupTopicTokens(groupType),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return output.Ids;
    }

    /// <inheritdoc/>
    public async Task<Models.Group> GetGroupAsync(string groupType, GroupId groupId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.GetGroupInputArguments request = new() { GroupId = groupId.Value };

        var output = await _stub.GetGroupAsync(
            request,
            requestMetadata: null,
            additionalTopicTokenMap: GroupTopicTokens(groupType),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<Models.Group> CreateGroupAsync(string groupType, GroupId groupId, Models.GroupAttributes attributes, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.GroupAttributes request = Converter.ToGenerated(attributes);
        request.GroupId = groupId.Value;

        var output = await _stub.CreateGroupAsync(
            request,
            requestMetadata: null,
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
            requestMetadata: null,
            additionalTopicTokenMap: GroupTopicTokens(groupType),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);
    }

    // ---- Resource APIs ----

    /// <inheritdoc/>
    public async Task<Models.Resource> GetResourceAsync(string groupType, GroupId groupId, string resourceType, string resourceId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.GetResourceInputArguments request = new() { GroupId = groupId.Value };

        var output = await _stub.GetResourceAsync(
            request,
            requestMetadata: null,
            additionalTopicTokenMap: ResourceTopicTokens(groupType, resourceType, resourceId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<Models.Resource> CreateResourceAsync(string groupType, GroupId groupId, string resourceType, string resourceId, Models.ResourceMetaAttributes meta, Dictionary<string, byte[]> resourceExtensions, CreateVersionId defaultVersionId, Models.VersionAttributes defaultVersion, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
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
            requestMetadata: null,
            additionalTopicTokenMap: ResourceTopicTokens(groupType, resourceType, resourceId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Models.ResourceXid>> ListResourcesAsync(GroupQuery groups, string? resourceType = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
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
            requestMetadata: null,
            additionalTopicTokenMap: null,
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
            requestMetadata: null,
            additionalTopicTokenMap: ResourceTopicTokens(groupType, resourceType, resourceId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);
    }

    // ---- Version APIs ----

    /// <inheritdoc/>
    public Task<Models.Version> GetVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, GetVersionId versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task<Models.Version> CreateVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, IReadOnlyList<Models.Label> resourceLabels, CreateVersionId versionId, Models.VersionAttributes version, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task<IReadOnlyList<Models.VersionXid>> ListVersionsAsync(GroupQuery groups, string? resourceType = null, string? resourceId = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task DeleteVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, string versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

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
