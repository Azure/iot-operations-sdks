// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// <see cref="EdgeRegistryClient"/> implementation of the xRegistry Schema extension surface
/// (<see cref="ISchemaRegistryClient"/>): create, retrieve, list, and delete Schema Versions.
/// </summary>
public sealed partial class EdgeRegistryClient : ISchemaRegistryClient
{
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
    public async Task<IReadOnlyList<Models.SchemaVersionXid>> ListSchemaVersionsAsync(GroupSelector groups, string? schemaId = null, string? documentHash = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
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
            DocumentHash = documentHash,
            Label = label is null ? null : Converter.ToGenerated(label),
        };

        var output = await _schemaStub.ListSchemaVersionsAsync(
            request,
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);

        return Converter.ToModel(output);
    }

    /// <inheritdoc/>
    public async Task DeleteSchemaVersionAsync(GroupId groupId, string schemaId, ulong versionId, Models.DeleteOptions? options = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        DeleteSchemaVersionInputArguments request = new() { GroupId = groupId.Value, Options = Converter.ToGenerated(options ?? Models.DeleteOptions.Default) };

        await _schemaStub.DeleteSchemaVersionAsync(
            request,
            additionalTopicTokenMap: ExtensionVersionTopicTokens(SchemaIdTopicToken, schemaId, versionId),
            commandTimeout: timeout ?? s_defaultCommandTimeout,
            cancellationToken: cancellationToken);
    }
}
