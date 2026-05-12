// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.EdgeRegistry.Generated;
using Azure.Iot.Operations.Services.EdgeRegistry.Models;

namespace Azure.Iot.Operations.Services.EdgeRegistry;

public interface IEdgeRegistryClient : IAsyncDisposable
{
    // -----------------------------------------------------------------------
    // Generic operations
    // -----------------------------------------------------------------------

    /// <summary>
    /// List entity IDs at the given xId path.
    /// </summary>
    /// <param name="xid">XID path of the collection to list (e.g. /schemagroups, /schemagroups/g1/schemas).</param>
    /// <param name="timeout">Command timeout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of entity IDs.</returns>
    Task<List<string>> ListAsync(string xid, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    // -----------------------------------------------------------------------
    // Group operations
    // -----------------------------------------------------------------------

    /// <summary>
    /// List group IDs of the given type.
    /// </summary>
    Task<List<string>> ListGroupsAsync(string groupType, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a group by type and ID.
    /// </summary>
    Task<Group> GetGroupAsync(string groupType, string groupId, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a group.
    /// </summary>
    Task<Group> CreateGroupAsync(string groupType, string groupId, GroupCreateAttributes? attributes = null, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    // -----------------------------------------------------------------------
    // Resource operations
    // -----------------------------------------------------------------------

    /// <summary>
    /// List resource IDs within a group.
    /// </summary>
    Task<List<string>> ListResourcesAsync(string groupType, string groupId, string resourceType, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List version IDs for a resource.
    /// </summary>
    Task<List<string>> ListVersionsAsync(string groupType, string groupId, string resourceType, string resourceId, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    // -----------------------------------------------------------------------
    // Schema group operations
    // -----------------------------------------------------------------------

    /// <summary>
    /// List schema group IDs.
    /// </summary>
    Task<List<string>> ListSchemaGroupsAsync(TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the schema group for this client's namespace.
    /// </summary>
    Task<Group> GetSchemaGroupAsync(TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create the schema group for this client's namespace.
    /// </summary>
    Task<Group> CreateSchemaGroupAsync(GroupCreateAttributes? attributes = null, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    // -----------------------------------------------------------------------
    // Schema operations
    // -----------------------------------------------------------------------

    /// <summary>
    /// List schema IDs in the schema group.
    /// </summary>
    Task<List<string>> ListSchemasAsync(TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a schema by ID.
    /// </summary>
    Task<Schema> GetSchemaAsync(string schemaId, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a schema with an initial version.
    /// </summary>
    Task<Schema> CreateSchemaAsync(string schemaId, CreateSchemaVersionOptions versionOptions, ResourceMetaCreateAttributes? metaAttributes = null, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List version IDs for a schema.
    /// </summary>
    Task<List<ulong>> ListSchemaVersionsAsync(string schemaId, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific schema version.
    /// </summary>
    Task<SchemaVersion> GetSchemaVersionAsync(string schemaId, ulong versionId, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new schema version.
    /// </summary>
    Task<SchemaVersion> CreateSchemaVersionAsync(string schemaId, CreateSchemaVersionOptions versionOptions, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    // -----------------------------------------------------------------------
    // Thing Description group operations
    // -----------------------------------------------------------------------

    /// <summary>
    /// List thing description group IDs.
    /// </summary>
    Task<List<string>> ListThingDescriptionGroupsAsync(TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the thing description group for this client's namespace.
    /// </summary>
    Task<Group> GetThingDescriptionGroupAsync(TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create the thing description group for this client's namespace.
    /// </summary>
    Task<Group> CreateThingDescriptionGroupAsync(GroupCreateAttributes? attributes = null, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    // -----------------------------------------------------------------------
    // Thing Description operations
    // -----------------------------------------------------------------------

    /// <summary>
    /// List thing description IDs in the thing description group.
    /// </summary>
    Task<List<string>> ListThingDescriptionsAsync(TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a thing description by ID.
    /// </summary>
    Task<ThingDescription> GetThingDescriptionAsync(string thingDescriptionId, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a thing description with an initial version.
    /// </summary>
    Task<ThingDescription> CreateThingDescriptionAsync(string thingDescriptionId, CreateThingDescriptionVersionOptions versionOptions, ResourceMetaCreateAttributes? metaAttributes = null, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List version IDs for a thing description.
    /// </summary>
    Task<List<string>> ListThingDescriptionVersionsAsync(string thingDescriptionId, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific thing description version.
    /// </summary>
    Task<ThingDescriptionVersion> GetThingDescriptionVersionAsync(string thingDescriptionId, string versionId, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new thing description version.
    /// </summary>
    Task<ThingDescriptionVersion> CreateThingDescriptionVersionAsync(string thingDescriptionId, CreateThingDescriptionVersionOptions versionOptions, TimeSpan? timeout = default, CancellationToken cancellationToken = default);

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    /// <summary>
    /// Make this client unsubscribe from any topics that it subscribed to.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously dispose of this client and optionally dispose the underlying MQTT client.
    /// </summary>
    ValueTask DisposeAsync(bool disposing, CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously dispose of this client and optionally dispose the underlying MQTT client.
    /// </summary>
    ValueTask DisposeAsync(bool disposing);

    /// <summary>
    /// Asynchronously dispose this object, but not the underlying MQTT client.
    /// </summary>
    ValueTask DisposeAsync(CancellationToken cancellationToken);
}
