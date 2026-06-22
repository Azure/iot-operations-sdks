// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Default <see cref="IEdgeRegistryClient"/> implementation. Wraps the generated core, Schema, Thing
/// Description, and Thing Model extension xRegistry RPC clients (CoreClientStub, SchemaClientStub,
/// ThingDescriptionClientStub, ThingModelClientStub), routing XID components into per-call topic
/// tokens and mapping the generated wire types to the EdgeRegistry.Models domain types.
/// </summary>
public sealed class EdgeRegistryClient : IEdgeRegistryClient
{
    private const string SchemaIdTopicToken = "schemaId";
    private const string ThingDescriptionIdTopicToken = "thingDescriptionId";
    private const string ThingModelIdTopicToken = "thingModelId";

    private static readonly TimeSpan s_defaultCommandTimeout = TimeSpan.FromSeconds(10);

    private readonly CoreClientStub _coreStub;
    private readonly SchemaClientStub _schemaStub;
    private readonly ThingDescriptionClientStub _thingDescriptionStub;
    private readonly ThingModelClientStub _thingModelStub;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="EdgeRegistryClient"/> class.
    /// </summary>
    /// <param name="applicationContext">The shared application context.</param>
    /// <param name="mqttClient">The MQTT client used for RPC.</param>
    public EdgeRegistryClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
    {
        _coreStub = new CoreClientStub(applicationContext, mqttClient);
        _schemaStub = new SchemaClientStub(applicationContext, mqttClient);
        _thingDescriptionStub = new ThingDescriptionClientStub(applicationContext, mqttClient);
        _thingModelStub = new ThingModelClientStub(applicationContext, mqttClient);
    }

    // ---- Group APIs ----

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListGroupsAsync(string groupType, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        var output = await _coreStub.ListGroupsAsync(
            requestMetadata: null,
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

        var output = await _coreStub.GetGroupAsync(
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

        var output = await _coreStub.CreateGroupAsync(
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

        await _coreStub.DeleteGroupAsync(
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

        var output = await _coreStub.GetResourceAsync(
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
    public async Task DeleteResourceAsync(string groupType, GroupId groupId, string resourceType, string resourceId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.DeleteResourceInputArguments request = new() { GroupId = groupId.Value };

        await _coreStub.DeleteResourceAsync(
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

        var output = await _coreStub.GetVersionAsync(
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
    public async Task DeleteVersionAsync(string groupType, GroupId groupId, string resourceType, string resourceId, string versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.DeleteVersionInputArguments request = new() { GroupId = groupId.Value };

        await _coreStub.DeleteVersionAsync(
            request,
            additionalTopicTokenMap: VersionTopicTokens(groupType, resourceType, resourceId, versionId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);
    }

    // ---- Schema extension APIs ----

    /// <inheritdoc/>
    public async Task<Models.SchemaVersion> CreateSchemaVersionAsync(GroupId groupId, string schemaId, IReadOnlyList<Models.Label> schemaLabels, Models.SchemaVersionAttributes version, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.CreateSchemaVersionAttributes request = Converter.ToGenerated(version, groupId.Value, schemaLabels);

        var output = await _schemaStub.CreateSchemaVersionAsync(
            request,
            additionalTopicTokenMap: ExtensionResourceTopicTokens(SchemaIdTopicToken, schemaId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<Models.SchemaVersion> GetSchemaVersionAsync(GroupId groupId, string schemaId, GetSchemaVersionId versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.GetSchemaVersionInputArguments request = new()
        {
            GroupId = groupId.Value,
            VersionId = versionId.Value,
        };

        var output = await _schemaStub.GetSchemaVersionAsync(
            request,
            additionalTopicTokenMap: ExtensionResourceTopicTokens(SchemaIdTopicToken, schemaId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Models.SchemaVersionXid>> ListSchemaVersionsAsync(GroupSelector groups, string? schemaId = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        (string? groupId, bool allGroups) = groups.Resolve();
        Generated.ListVersionsRequestPayload request = new()
        {
            GroupType = Generated.Constants.SchemaGroupType,
            GroupId = groupId,
            AllGroups = allGroups,
            ResourceType = Generated.Constants.SchemaResourceType,
            ResourceId = schemaId,
            Label = label is null ? null : Converter.ToGenerated(label),
        };

        var output = await _schemaStub.ListSchemaVersionsAsync(
            request,
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task DeleteSchemaVersionAsync(GroupId groupId, string schemaId, ulong versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.DeleteSchemaVersionInputArguments request = new() { GroupId = groupId.Value };

        await _schemaStub.DeleteSchemaVersionAsync(
            request,
            additionalTopicTokenMap: ExtensionVersionTopicTokens(SchemaIdTopicToken, schemaId, versionId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);
    }

    // ---- Thing Description extension APIs ----

    /// <inheritdoc/>
    public async Task<Models.ThingDescriptionVersion> CreateThingDescriptionVersionAsync(GroupId groupId, string thingDescriptionId, IReadOnlyList<Models.Label> thingDescriptionLabels, Models.ThingDescriptionVersionAttributes version, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.CreateThingDescriptionVersionAttributes request = Converter.ToGenerated(version, groupId.Value, thingDescriptionLabels);

        var output = await _thingDescriptionStub.CreateThingDescriptionVersionAsync(
            request,
            additionalTopicTokenMap: ExtensionResourceTopicTokens(ThingDescriptionIdTopicToken, thingDescriptionId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<Models.ThingDescriptionVersion> GetThingDescriptionVersionAsync(GroupId groupId, string thingDescriptionId, GetThingDescriptionVersionId versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.GetThingDescriptionVersionInputArguments request = new()
        {
            GroupId = groupId.Value,
            VersionId = versionId.Value,
        };

        var output = await _thingDescriptionStub.GetThingDescriptionVersionAsync(
            request,
            additionalTopicTokenMap: ExtensionResourceTopicTokens(ThingDescriptionIdTopicToken, thingDescriptionId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Models.ThingDescriptionVersionXid>> ListThingDescriptionVersionsAsync(GroupSelector groups, string? thingDescriptionId = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        (string? groupId, bool allGroups) = groups.Resolve();
        Generated.ListVersionsRequestPayload request = new()
        {
            GroupType = Generated.Constants.ThingDescriptionGroupType,
            GroupId = groupId,
            AllGroups = allGroups,
            ResourceType = Generated.Constants.ThingDescriptionResourceType,
            ResourceId = thingDescriptionId,
            Label = label is null ? null : Converter.ToGenerated(label),
        };

        var output = await _thingDescriptionStub.ListThingDescriptionVersionsAsync(
            request,
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task DeleteThingDescriptionVersionAsync(GroupId groupId, string thingDescriptionId, ulong versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.DeleteThingDescriptionVersionInputArguments request = new() { GroupId = groupId.Value };

        await _thingDescriptionStub.DeleteThingDescriptionVersionAsync(
            request,
            additionalTopicTokenMap: ExtensionVersionTopicTokens(ThingDescriptionIdTopicToken, thingDescriptionId, versionId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);
    }

    // ---- Thing Model extension APIs ----

    /// <inheritdoc/>
    public async Task<Models.ThingModelVersion> CreateThingModelVersionAsync(GroupId groupId, string thingModelId, IReadOnlyList<Models.Label> thingModelLabels, Models.ThingModelVersionAttributes version, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.CreateThingModelVersionAttributes request = Converter.ToGenerated(version, groupId.Value, thingModelLabels);

        var output = await _thingModelStub.CreateThingModelVersionAsync(
            request,
            additionalTopicTokenMap: ExtensionResourceTopicTokens(ThingModelIdTopicToken, thingModelId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<Models.ThingModelVersion> GetThingModelVersionAsync(GroupId groupId, string thingModelId, GetThingModelVersionId versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.GetThingModelVersionInputArguments request = new()
        {
            GroupId = groupId.Value,
            VersionId = versionId.Value,
        };

        var output = await _thingModelStub.GetThingModelVersionAsync(
            request,
            additionalTopicTokenMap: ExtensionResourceTopicTokens(ThingModelIdTopicToken, thingModelId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Models.ThingModelVersionXid>> ListThingModelVersionsAsync(GroupSelector groups, string? thingModelId = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        (string? groupId, bool allGroups) = groups.Resolve();
        Generated.ListVersionsRequestPayload request = new()
        {
            GroupType = Generated.Constants.ThingModelGroupType,
            GroupId = groupId,
            AllGroups = allGroups,
            ResourceType = Generated.Constants.ThingModelResourceType,
            ResourceId = thingModelId,
            Label = label is null ? null : Converter.ToGenerated(label),
        };

        var output = await _thingModelStub.ListThingModelVersionsAsync(
            request,
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task DeleteThingModelVersionAsync(GroupId groupId, string thingModelId, ulong versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        Generated.DeleteThingModelVersionInputArguments request = new() { GroupId = groupId.Value };

        await _thingModelStub.DeleteThingModelVersionAsync(
            request,
            additionalTopicTokenMap: ExtensionVersionTopicTokens(ThingModelIdTopicToken, thingModelId, versionId),
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

    /// <summary>Builds the topic tokens for an extension Resource-scoped request, keyed by the extension's Resource-identifier token (e.g. <c>schemaId</c>).</summary>
    private static Dictionary<string, string> ExtensionResourceTopicTokens(string resourceIdToken, string resourceId)
        => new() { [resourceIdToken] = resourceId };

    /// <summary>Builds the topic tokens for an extension Version-scoped request that carries the Version id in the topic.</summary>
    private static Dictionary<string, string> ExtensionVersionTopicTokens(string resourceIdToken, string resourceId, ulong versionId)
    {
        Dictionary<string, string> tokens = ExtensionResourceTopicTokens(resourceIdToken, resourceId);
        tokens["versionId"] = versionId.ToString(CultureInfo.InvariantCulture);
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
        await _coreStub.DisposeAsync().ConfigureAwait(false);
        await _schemaStub.DisposeAsync().ConfigureAwait(false);
        await _thingDescriptionStub.DisposeAsync().ConfigureAwait(false);
        await _thingModelStub.DisposeAsync().ConfigureAwait(false);
    }
}
