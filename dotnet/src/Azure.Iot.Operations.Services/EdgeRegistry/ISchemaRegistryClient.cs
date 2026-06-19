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
    Task<Models.SchemaVersion> CreateSchemaVersionAsync(GroupId groupId, string schemaId, IReadOnlyList<Models.Label> schemaLabels, Models.SchemaVersionAttributes version, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a Schema Version. Pass <see cref="GetSchemaVersionId.ResourceDefault"/> for the Schema's default (latest) Version.</summary>
    Task<Models.SchemaVersion> GetSchemaVersionAsync(GroupId groupId, string schemaId, GetSchemaVersionId versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Lists the XIDs of Schema Versions matching the query, optionally filtered by Schema id and/or a single label.</summary>
    Task<IReadOnlyList<Models.SchemaVersionXid>> ListSchemaVersionsAsync(GroupSelector groups, string? schemaId = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a specific Schema Version.</summary>
    Task DeleteSchemaVersionAsync(GroupId groupId, string schemaId, ulong versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
