// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Client for the xRegistry Schema extension: create, retrieve, list, and delete Schema Versions.
/// Schema Versions use integer (<see cref="ulong"/>) version identifiers assigned by the service.
/// The parent Schema (Resource) and Schema Group are implicitly created when a Version is created.
/// </summary>
/// <remarks>
/// <para>
/// The Schema extension is the core xRegistry model specialized for schemas; the generic concepts
/// map directly (see <see cref="ICoreClient"/>):
/// </para>
/// <list type="bullet">
/// <item><description>Schema Group = xRegistry Group (<c>schemagroups</c>).</description></item>
/// <item><description>Schema = xRegistry Resource — the named schema that owns its revisions.</description></item>
/// <item><description>
/// Schema Version = xRegistry Version — one immutable revision of a schema document.
/// <see cref="Models.SchemaVersion"/> carries the document bytes plus the revision's metadata; its
/// <c>versionId</c> is a service-assigned integer, not a semantic version.
/// </description></item>
/// </list>
/// </remarks>
public interface ISchemaRegistryClient
{
    /// <summary>Creates a Schema Version under the given Schema, implicitly creating the parent Schema if needed.</summary>
    /// <param name="groupId">The Schema Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="schemaId">The Schema (Resource) identifier.</param>
    /// <param name="schemaLabels">Labels applied to the parent Schema.</param>
    /// <param name="version">The attributes of the Schema Version to create.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the created <see cref="Models.SchemaVersion"/>.</returns>
    Task<Models.SchemaVersion> CreateSchemaVersionAsync(GroupId groupId, string schemaId, IReadOnlyList<Models.Label> schemaLabels, Models.SchemaVersionAttributes version, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a Schema Version.</summary>
    /// <param name="groupId">The Schema Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="schemaId">The Schema (Resource) identifier.</param>
    /// <param name="versionId">The Version to retrieve. Use <see cref="GetSchemaVersionId.ResourceDefault"/> for the Schema's default (latest) Version.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the requested <see cref="Models.SchemaVersion"/>.</returns>
    Task<Models.SchemaVersion> GetSchemaVersionAsync(GroupId groupId, string schemaId, GetSchemaVersionId versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Lists the XIDs of Schema Versions matching the query, optionally filtered by Schema id and/or a single label.</summary>
    /// <param name="groups">The Groups to search; see <see cref="GroupSelector"/> for the available scopes.</param>
    /// <param name="schemaId">When set, restricts the results to this Schema.</param>
    /// <param name="documentHash">When set, restricts the results to entities whose document has this hash.</param>
    /// <param name="label">When set, restricts the results to entities carrying this label.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task whose result is the XIDs of the matching Schema Versions.</returns>
    Task<IReadOnlyList<Models.SchemaVersionXid>> ListSchemaVersionsAsync(GroupSelector groups, string? schemaId = null, string? documentHash = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a specific Schema Version.</summary>
    /// <param name="groupId">The Schema Group. Use <see cref="GroupId.CloudDefault"/> for the cloud-default Group (the configured namespace).</param>
    /// <param name="schemaId">The Schema (Resource) identifier.</param>
    /// <param name="versionId">The identifier of the Schema Version to delete.</param>
    /// <param name="options">The <see cref="Models.DeleteOptions"/> that control the behavior of the delete operation; when <see langword="null"/>, <see cref="Models.DeleteOptions.Default"/> is used.</param>
    /// <param name="timeout">The command timeout; when <see langword="null"/>, the client's default timeout is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the Schema Version has been deleted.</returns>
    Task DeleteSchemaVersionAsync(GroupId groupId, string schemaId, ulong versionId, Models.DeleteOptions? options = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
