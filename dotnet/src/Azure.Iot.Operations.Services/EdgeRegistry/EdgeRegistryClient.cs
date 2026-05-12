// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.EdgeRegistry.Generated;
using Azure.Iot.Operations.Services.EdgeRegistry.Models;

namespace Azure.Iot.Operations.Services.EdgeRegistry;

public class EdgeRegistryClient : IEdgeRegistryClient
{
    private static readonly TimeSpan s_DefaultCommandTimeout = TimeSpan.FromSeconds(10);

    private readonly string _namespace;
    private readonly EdgeRegistryClientStub _edgeRegistryStub;
    private readonly EdgeRegistrySchemaExtensionsClientStub _schemaExtensionsStub;
    private readonly EdgeRegistryThingDescriptionExtensionsClientStub _thingDescriptionExtensionsStub;
    private bool _disposed;

    /// <summary>
    /// Construct a new Edge Registry client.
    /// </summary>
    /// <param name="applicationContext">The shared context for your application.</param>
    /// <param name="mqttClient">The MQTT client to use.</param>
    /// <param name="namespace">The namespace used as the group ID for schema and thing description operations.</param>
    public EdgeRegistryClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, string @namespace)
    {
        _namespace = @namespace;
        _edgeRegistryStub = new(applicationContext, mqttClient);
        _schemaExtensionsStub = new(applicationContext, mqttClient);
        _thingDescriptionExtensionsStub = new(applicationContext, mqttClient);
    }

    // -----------------------------------------------------------------------
    // Generic operations
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<List<string>> ListAsync(string xid, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            ListOutputArguments result = await _edgeRegistryStub.ListAsync(
                new ListInputArguments { Xid = xid },
                commandTimeout: timeout ?? s_DefaultCommandTimeout,
                cancellationToken: cancellationToken);

            return result.Ids;
        }
        catch (EdgeRegistryErrorException ex)
        {
            throw new EdgeRegistryServiceException(Converter.ToServiceError(ex.EdgeRegistryError), ex);
        }
    }

    // -----------------------------------------------------------------------
    // Group operations
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<List<string>> ListGroupsAsync(string groupType, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        return await ListAsync($"/{groupType}", timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Group> GetGroupAsync(string groupType, string groupId, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            return await _edgeRegistryStub.GetGroupAsync(
                new GetGroupInputArguments { GroupType = groupType, GroupId = groupId },
                commandTimeout: timeout ?? s_DefaultCommandTimeout,
                cancellationToken: cancellationToken);
        }
        catch (EdgeRegistryErrorException ex)
        {
            throw new EdgeRegistryServiceException(Converter.ToServiceError(ex.EdgeRegistryError), ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Group> CreateGroupAsync(string groupType, string groupId, GroupCreateAttributes? attributes = null, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            return await _edgeRegistryStub.CreateGroupAsync(
                new Generated.GroupAttributes
                {
                    GroupType = groupType,
                    GroupId = groupId,
                    Name = attributes?.Name,
                    Description = attributes?.Description,
                    Documentation = attributes?.Documentation,
                    Labels = attributes?.Labels ?? new(),
                    Extensions = attributes?.Extensions ?? new(),
                },
                commandTimeout: timeout ?? s_DefaultCommandTimeout,
                cancellationToken: cancellationToken);
        }
        catch (EdgeRegistryErrorException ex)
        {
            throw new EdgeRegistryServiceException(Converter.ToServiceError(ex.EdgeRegistryError), ex);
        }
    }

    // -----------------------------------------------------------------------
    // Resource operations
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<List<string>> ListResourcesAsync(string groupType, string groupId, string resourceType, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        return await ListAsync($"/{groupType}/{groupId}/{resourceType}", timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ListVersionsAsync(string groupType, string groupId, string resourceType, string resourceId, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        return await ListAsync($"/{groupType}/{groupId}/{resourceType}/{resourceId}/versions", timeout, cancellationToken).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Schema group operations
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<List<string>> ListSchemaGroupsAsync(TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        return await ListGroupsAsync(Constants.SchemaGroupType, timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Group> GetSchemaGroupAsync(TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        return await GetGroupAsync(Constants.SchemaGroupType, _namespace, timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Group> CreateSchemaGroupAsync(GroupCreateAttributes? attributes = null, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        return await CreateGroupAsync(Constants.SchemaGroupType, _namespace, attributes, timeout, cancellationToken).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Schema operations
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<List<string>> ListSchemasAsync(TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        return await ListResourcesAsync(Constants.SchemaGroupType, _namespace, Constants.SchemaResourceType, timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Schema> GetSchemaAsync(string schemaId, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            return await _schemaExtensionsStub.GetSchemaAsync(
                new GetSchemaInputArguments { GroupId = _namespace, SchemaId = schemaId },
                commandTimeout: timeout ?? s_DefaultCommandTimeout,
                cancellationToken: cancellationToken);
        }
        catch (SchemaExtensionErrorException ex)
        {
            throw new EdgeRegistryServiceException(Converter.ToServiceError(ex.SchemaExtensionError), ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Schema> CreateSchemaAsync(string schemaId, CreateSchemaVersionOptions versionOptions, ResourceMetaCreateAttributes? metaAttributes = null, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            return await _schemaExtensionsStub.CreateSchemaAsync(
                new CreateSchemaAttributes
                {
                    SchemaMetaAttributes = new Generated.ResourceMetaAttributes
                    {
                        Id = schemaId,
                        Labels = metaAttributes?.Labels ?? new(),
                        Extensions = metaAttributes?.Extensions ?? new(),
                    },
                    CreateSchemaVersionAttributes = new Generated.CreateSchemaVersionAttributes
                    {
                        GroupId = _namespace,
                        SchemaId = schemaId,
                        Format = versionOptions.Format,
                        SchemaDocument = versionOptions.SchemaDocument,
                        Ancestor = versionOptions.Ancestor,
                        ContentType = versionOptions.ContentType,
                        Description = versionOptions.Description,
                        Documentation = versionOptions.Documentation,
                        Labels = versionOptions.Labels ?? new(),
                        Name = versionOptions.Name,
                    },
                },
                commandTimeout: timeout ?? s_DefaultCommandTimeout,
                cancellationToken: cancellationToken);
        }
        catch (SchemaExtensionErrorException ex)
        {
            throw new EdgeRegistryServiceException(Converter.ToServiceError(ex.SchemaExtensionError), ex);
        }
    }

    /// <inheritdoc/>
    public async Task<List<ulong>> ListSchemaVersionsAsync(string schemaId, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            ListSchemaVersionsOutputArguments result = await _schemaExtensionsStub.ListSchemaVersionsAsync(
                new ListSchemaVersionsInputArguments { GroupId = _namespace, SchemaId = schemaId },
                commandTimeout: timeout ?? s_DefaultCommandTimeout,
                cancellationToken: cancellationToken);

            return result.Ids;
        }
        catch (SchemaExtensionErrorException ex)
        {
            throw new EdgeRegistryServiceException(Converter.ToServiceError(ex.SchemaExtensionError), ex);
        }
    }

    /// <inheritdoc/>
    public async Task<SchemaVersion> GetSchemaVersionAsync(string schemaId, ulong versionId, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            return await _schemaExtensionsStub.GetSchemaVersionAsync(
                new GetSchemaVersionInputArguments { GroupId = _namespace, SchemaId = schemaId, VersionId = versionId },
                commandTimeout: timeout ?? s_DefaultCommandTimeout,
                cancellationToken: cancellationToken);
        }
        catch (SchemaExtensionErrorException ex)
        {
            throw new EdgeRegistryServiceException(Converter.ToServiceError(ex.SchemaExtensionError), ex);
        }
    }

    /// <inheritdoc/>
    public async Task<SchemaVersion> CreateSchemaVersionAsync(string schemaId, CreateSchemaVersionOptions versionOptions, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            return await _schemaExtensionsStub.CreateSchemaVersionAsync(
                new Generated.CreateSchemaVersionAttributes
                {
                    GroupId = _namespace,
                    SchemaId = schemaId,
                    Format = versionOptions.Format,
                    SchemaDocument = versionOptions.SchemaDocument,
                    Ancestor = versionOptions.Ancestor,
                    ContentType = versionOptions.ContentType,
                    Description = versionOptions.Description,
                    Documentation = versionOptions.Documentation,
                    Labels = versionOptions.Labels ?? new(),
                    Name = versionOptions.Name,
                },
                commandTimeout: timeout ?? s_DefaultCommandTimeout,
                cancellationToken: cancellationToken);
        }
        catch (SchemaExtensionErrorException ex)
        {
            throw new EdgeRegistryServiceException(Converter.ToServiceError(ex.SchemaExtensionError), ex);
        }
    }

    // -----------------------------------------------------------------------
    // Thing Description group operations
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<List<string>> ListThingDescriptionGroupsAsync(TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        return await ListGroupsAsync(Constants.ThingDescriptionGroupType, timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Group> GetThingDescriptionGroupAsync(TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        return await GetGroupAsync(Constants.ThingDescriptionGroupType, _namespace, timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Group> CreateThingDescriptionGroupAsync(GroupCreateAttributes? attributes = null, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        return await CreateGroupAsync(Constants.ThingDescriptionGroupType, _namespace, attributes, timeout, cancellationToken).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Thing Description operations
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<List<string>> ListThingDescriptionsAsync(TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        return await ListResourcesAsync(Constants.ThingDescriptionGroupType, _namespace, Constants.ThingDescriptionResourceType, timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ThingDescription> GetThingDescriptionAsync(string thingDescriptionId, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            return await _thingDescriptionExtensionsStub.GetThingDescriptionAsync(
                new GetThingDescriptionInputArguments { GroupId = _namespace, ThingDescriptionId = thingDescriptionId },
                commandTimeout: timeout ?? s_DefaultCommandTimeout,
                cancellationToken: cancellationToken);
        }
        catch (ThingDescriptionExtensionErrorException ex)
        {
            throw new EdgeRegistryServiceException(Converter.ToServiceError(ex.ThingDescriptionExtensionError), ex);
        }
    }

    /// <inheritdoc/>
    public async Task<ThingDescription> CreateThingDescriptionAsync(string thingDescriptionId, CreateThingDescriptionVersionOptions versionOptions, ResourceMetaCreateAttributes? metaAttributes = null, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            return await _thingDescriptionExtensionsStub.CreateThingDescriptionAsync(
                new CreateThingDescriptionAttributes
                {
                    ThingDescriptionMetaAttributes = new Generated.ResourceMetaAttributes
                    {
                        Id = thingDescriptionId,
                        Labels = metaAttributes?.Labels ?? new(),
                        Extensions = metaAttributes?.Extensions ?? new(),
                    },
                    CreateThingDescriptionVersionAttributes = new Generated.CreateThingDescriptionVersionAttributes
                    {
                        GroupId = _namespace,
                        ThingDescriptionId = thingDescriptionId,
                        VersionId = versionOptions.VersionId,
                        Format = versionOptions.Format,
                        ThingDescriptionDocument = versionOptions.ThingDescriptionDocument,
                        Ancestor = versionOptions.Ancestor,
                        ContentType = versionOptions.ContentType,
                        Description = versionOptions.Description,
                        Documentation = versionOptions.Documentation,
                        Labels = versionOptions.Labels ?? new(),
                        Name = versionOptions.Name,
                    },
                },
                commandTimeout: timeout ?? s_DefaultCommandTimeout,
                cancellationToken: cancellationToken);
        }
        catch (ThingDescriptionExtensionErrorException ex)
        {
            throw new EdgeRegistryServiceException(Converter.ToServiceError(ex.ThingDescriptionExtensionError), ex);
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> ListThingDescriptionVersionsAsync(string thingDescriptionId, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        return await ListVersionsAsync(
            Constants.ThingDescriptionGroupType,
            _namespace,
            Constants.ThingDescriptionResourceType,
            thingDescriptionId,
            timeout,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ThingDescriptionVersion> GetThingDescriptionVersionAsync(string thingDescriptionId, string versionId, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            return await _thingDescriptionExtensionsStub.GetThingDescriptionVersionAsync(
                new GetThingDescriptionVersionInputArguments { GroupId = _namespace, ThingDescriptionId = thingDescriptionId, VersionId = versionId },
                commandTimeout: timeout ?? s_DefaultCommandTimeout,
                cancellationToken: cancellationToken);
        }
        catch (ThingDescriptionExtensionErrorException ex)
        {
            throw new EdgeRegistryServiceException(Converter.ToServiceError(ex.ThingDescriptionExtensionError), ex);
        }
    }

    /// <inheritdoc/>
    public async Task<ThingDescriptionVersion> CreateThingDescriptionVersionAsync(string thingDescriptionId, CreateThingDescriptionVersionOptions versionOptions, TimeSpan? timeout = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            return await _thingDescriptionExtensionsStub.CreateThingDescriptionVersionAsync(
                new Generated.CreateThingDescriptionVersionAttributes
                {
                    GroupId = _namespace,
                    ThingDescriptionId = thingDescriptionId,
                    VersionId = versionOptions.VersionId,
                    Format = versionOptions.Format,
                    ThingDescriptionDocument = versionOptions.ThingDescriptionDocument,
                    Ancestor = versionOptions.Ancestor,
                    ContentType = versionOptions.ContentType,
                    Description = versionOptions.Description,
                    Documentation = versionOptions.Documentation,
                    Labels = versionOptions.Labels ?? new(),
                    Name = versionOptions.Name,
                },
                commandTimeout: timeout ?? s_DefaultCommandTimeout,
                cancellationToken: cancellationToken);
        }
        catch (ThingDescriptionExtensionErrorException ex)
        {
            throw new EdgeRegistryServiceException(Converter.ToServiceError(ex.ThingDescriptionExtensionError), ex);
        }
    }

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            _edgeRegistryStub.StopAsync(cancellationToken),
            _schemaExtensionsStub.StopAsync(cancellationToken),
            _thingDescriptionExtensionsStub.StopAsync(cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously dispose this object, but not the underlying MQTT client.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore(false, CancellationToken.None).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync(CancellationToken cancellationToken)
    {
        await DisposeAsyncCore(false, cancellationToken).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync(bool disposing)
    {
        await DisposeAsyncCore(disposing, CancellationToken.None).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync(bool disposing, CancellationToken cancellationToken)
    {
        await DisposeAsyncCore(disposing, cancellationToken).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore(bool disposing, CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        await _edgeRegistryStub.DisposeAsync(disposing, cancellationToken).ConfigureAwait(false);
        await _schemaExtensionsStub.DisposeAsync(disposing, cancellationToken).ConfigureAwait(false);
        await _thingDescriptionExtensionsStub.DisposeAsync(disposing, cancellationToken).ConfigureAwait(false);

        _disposed = true;
    }
}
