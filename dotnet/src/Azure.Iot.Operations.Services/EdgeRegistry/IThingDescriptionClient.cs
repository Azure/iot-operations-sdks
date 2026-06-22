// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Client for the xRegistry Thing Description extension: create, retrieve, list, and delete Thing
/// Description Versions. Thing Description Versions use integer (<see cref="ulong"/>) version
/// identifiers assigned by the service. The parent Thing Description (Resource) and Thing Description
/// Group are implicitly created when a Version is created.
/// </summary>
/// <remarks>
/// <para>
/// The Thing Description extension is the core xRegistry model specialized for WoT Thing
/// Descriptions; the generic concepts map directly (see <see cref="ICoreClient"/>):
/// </para>
/// <list type="bullet">
/// <item><description>Thing Description Group = xRegistry Group (<c>thingdescriptiongroups</c>).</description></item>
/// <item><description>Thing Description = xRegistry Resource — the named Thing Description that owns its revisions.</description></item>
/// <item><description>
/// Thing Description Version = xRegistry Version — one immutable revision of a Thing Description
/// document. <see cref="Models.ThingDescriptionVersion"/> carries the document bytes plus the
/// revision's metadata; its <c>versionId</c> is a service-assigned integer, not a semantic version.
/// </description></item>
/// </list>
/// </remarks>
public interface IThingDescriptionClient
{
    /// <summary>Creates a Thing Description Version under the given Thing Description, implicitly creating the parent Thing Description if needed.</summary>
    Task<Models.ThingDescriptionVersion> CreateThingDescriptionVersionAsync(GroupId groupId, string thingDescriptionId, IReadOnlyList<Models.Label> thingDescriptionLabels, Models.ThingDescriptionVersionAttributes version, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a Thing Description Version. Pass <see cref="GetThingDescriptionVersionId.ResourceDefault"/> for the Thing Description's default (latest) Version.</summary>
    Task<Models.ThingDescriptionVersion> GetThingDescriptionVersionAsync(GroupId groupId, string thingDescriptionId, GetThingDescriptionVersionId versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Lists the XIDs of Thing Description Versions matching the query, optionally filtered by Thing Description id and/or a single label.</summary>
    Task<IReadOnlyList<Models.ThingDescriptionVersionXid>> ListThingDescriptionVersionsAsync(GroupSelector groups, string? thingDescriptionId = null, Models.Label? label = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a specific Thing Description Version.</summary>
    Task DeleteThingDescriptionVersionAsync(GroupId groupId, string thingDescriptionId, ulong versionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
