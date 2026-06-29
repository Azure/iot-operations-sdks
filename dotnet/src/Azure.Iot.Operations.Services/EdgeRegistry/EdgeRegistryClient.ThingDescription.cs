// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// <see cref="EdgeRegistryClient"/> implementation of the xRegistry Thing Description extension
/// surface (<see cref="IThingDescriptionClient"/>): create, retrieve, list, and delete Thing
/// Description Versions.
/// </summary>
public sealed partial class EdgeRegistryClient : IThingDescriptionClient
{
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
    public async Task DeleteThingDescriptionVersionAsync(GroupId groupId, string thingDescriptionId, ulong versionId, Models.DeleteOptions options, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        DeleteThingDescriptionVersionInputArguments request = new() { GroupId = groupId.Value, Options = Converter.ToGenerated(options) };

        await _thingDescriptionStub.DeleteThingDescriptionVersionAsync(
            request,
            additionalTopicTokenMap: ExtensionVersionTopicTokens(ThingDescriptionIdTopicToken, thingDescriptionId, versionId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);
    }
}
