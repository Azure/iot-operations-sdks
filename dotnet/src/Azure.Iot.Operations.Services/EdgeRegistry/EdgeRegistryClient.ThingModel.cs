// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// <see cref="EdgeRegistryClient"/> implementation of the xRegistry Thing Model extension surface
/// (<see cref="IThingModelClient"/>): create, retrieve, list, and delete Thing Model Versions.
/// </summary>
public sealed partial class EdgeRegistryClient : IThingModelClient
{
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
    public async Task<IReadOnlyList<Models.ThingModelVersionXid>> ListThingModelVersionsAsync(GroupSelector groups, string? thingModelId = null, string? documentHash = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
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
    public async Task DeleteThingModelVersionAsync(GroupId groupId, string thingModelId, ulong versionId, Models.DeleteOptions? options = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        DeleteThingModelVersionInputArguments request = new() { GroupId = groupId.Value, Options = Converter.ToGenerated(options ?? Models.DeleteOptions.Default) };

        await _thingModelStub.DeleteThingModelVersionAsync(
            request,
            additionalTopicTokenMap: ExtensionVersionTopicTokens(ThingModelIdTopicToken, thingModelId, versionId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);
    }
}
