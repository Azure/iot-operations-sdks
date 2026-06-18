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

        Dictionary<string, string> topicTokens = new() { ["groupType"] = groupType };

        var output = await _stub.ListGroupsAsync(
            requestMetadata: null,
            additionalTopicTokenMap: topicTokens,
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return output.Ids;
    }

    /// <inheritdoc/>
    public async Task<Models.Group> GetGroupAsync(string groupType, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Dictionary<string, string> topicTokens = new() { ["groupType"] = groupType };
        Generated.GetGroupInputArguments request = new() { GroupId = groupId };

        var output = await _stub.GetGroupAsync(
            request,
            requestMetadata: null,
            additionalTopicTokenMap: topicTokens,
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<Models.Group> CreateGroupAsync(string groupType, Models.GroupAttributes attributes, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Dictionary<string, string> topicTokens = new() { ["groupType"] = groupType };
        Generated.GroupAttributes request = Converter.ToGenerated(attributes);
        request.GroupId = groupId;

        var output = await _stub.CreateGroupAsync(
            request,
            requestMetadata: null,
            additionalTopicTokenMap: topicTokens,
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task DeleteGroupAsync(string groupType, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Dictionary<string, string> topicTokens = new() { ["groupType"] = groupType };
        Generated.DeleteGroupInputArguments request = new() { GroupId = groupId };

        await _stub.DeleteGroupAsync(
            request,
            requestMetadata: null,
            additionalTopicTokenMap: topicTokens,
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);
    }

    // ---- Resource APIs ----

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ListResourcesAsync(string groupType, string resourceType, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task<IReadOnlyList<Models.ResourceXid>> ListResourcesWithLabelAsync(string labelKey, string labelValue, string? groupType = null, string? groupId = null, string? resourceType = null, bool allGroups = false, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task<Models.Resource> GetResourceAsync(string groupType, string resourceType, string resourceId, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task<Models.Resource> CreateResourceAsync(string groupType, string resourceType, string resourceId, Models.ResourceMetaAttributes meta, Models.VersionAttributes defaultVersion, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task DeleteResourceAsync(string groupType, string resourceType, string resourceId, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    // ---- Version APIs ----

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ListVersionsAsync(string groupType, string resourceType, string resourceId, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task<IReadOnlyList<Models.VersionXid>> ListVersionsWithLabelAsync(string labelKey, string labelValue, string? groupType = null, string? groupId = null, string? resourceType = null, string? resourceId = null, bool allGroups = false, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task<Models.Version> GetVersionAsync(string groupType, string resourceType, string resourceId, string? versionId = null, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task<Models.Version> CreateVersionAsync(string groupType, string resourceType, string resourceId, Models.VersionAttributes version, IReadOnlyList<Models.Label> resourceLabels, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task DeleteVersionAsync(string groupType, string resourceType, string resourceId, string versionId, string? groupId = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

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
