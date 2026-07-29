// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Host;

using System.Globalization;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Services.EdgeRegistry.Host.Generated;
using Azure.Iot.Operations.Services.EdgeRegistry.Host.Generated.Common;
using static Azure.Iot.Operations.Services.EdgeRegistry.Host.ExtensionStub;

/// <summary>
/// In-memory implementation of the xRegistry Schema extension surface used by the integration tests.
/// </summary>
internal sealed class EdgeRegistrySchemaExtensionService : EdgeRegistrySchemaExtensions.Service
{
    private const string GroupType = "schemagroups";
    private const string ResourceType = "schemas";
    private const string SchemaIdToken = "ex:schemaId";

    private readonly ExtensionVersionStore<CreateSchemaVersionAttributes> _store = new(a => a.Labels, a => a.Document);

    public EdgeRegistrySchemaExtensionService(ApplicationContext applicationContext, MqttSessionClient mqttClient)
        : base(applicationContext, mqttClient)
    {
    }

    public override Task<ExtendedResponse<CreateSchemaVersionOutputArguments>> CreateSchemaVersionAsync(CreateSchemaVersionInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string schemaId = TopicToken(requestMetadata, SchemaIdToken);
        string groupId = request.GroupId ?? DefaultGroupId;
        var version = _store.CreateVersion(groupId, schemaId, request.SchemaLabels, request);
        return Task.FromResult(Ok(ToEntity(groupId, schemaId, version)));
    }

    public override Task<ExtendedResponse<GetSchemaVersionOutputArguments>> GetSchemaVersionAsync(GetSchemaVersionInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string schemaId = TopicToken(requestMetadata, SchemaIdToken);
        string groupId = request.GroupId ?? DefaultGroupId;
        var version = _store.GetVersion(groupId, schemaId, request.VersionId)
            ?? throw new SchemaExtensionErrorException(new SchemaExtensionError
            {
                Code = 404,
                Status = "Not Found",
                Title = "Schema Version not found",
                Type = "about:blank",
            });
        return Task.FromResult(Ok(ToEntity(groupId, schemaId, version)));
    }

    public override Task<ExtendedResponse<ListSchemaVersionsOutputArguments>> ListSchemaVersionsAsync(ListSchemaVersionsInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string? groupId = request.AllGroups ? null : (request.GroupId ?? DefaultGroupId);
        List<SchemaVersionXid> versions = _store
            .ListVersions(groupId, request.AllGroups, request.ResourceId, request.DocumentHash, request.Label)
            .Select(t => new SchemaVersionXid
            {
                GroupType = GroupType,
                GroupId = t.GroupId,
                ResourceType = ResourceType,
                ResourceId = t.ResourceId,
                VersionId = t.VersionId,
            })
            .ToList();
        return Task.FromResult(Ok(new SchemaVersionXidList { Versions = versions }));
    }

    public override Task<ExtendedResponse<EmptyJson>> DeleteSchemaVersionAsync(DeleteSchemaVersionInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string schemaId = TopicToken(requestMetadata, SchemaIdToken);
        ulong versionId = ulong.Parse(TopicToken(requestMetadata, VersionIdToken), CultureInfo.InvariantCulture);
        string groupId = request.GroupId ?? DefaultGroupId;
        _store.DeleteVersion(groupId, schemaId, versionId, request.Options.ExpectedEpoch);
        return Task.FromResult(Ok(new EmptyJson()));
    }

    private static SchemaVersion ToEntity(string groupId, string schemaId, ExtensionVersionStore<CreateSchemaVersionAttributes>.StoredVersion version)
    {
        CreateSchemaVersionAttributes a = version.Attributes;
        return new SchemaVersion
        {
            VersionId = version.VersionId,
            Ancestor = a.Ancestor ?? version.VersionId,
            Format = a.Format,
            Document = a.Document,
            DocumentHash = ComputeHash(a.Document),
            ResourceId = schemaId,
            Xid = $"/{GroupType}/{groupId}/{ResourceType}/{schemaId}/versions/{version.VersionId}",
            Epoch = version.Epoch,
            Name = a.Name,
            IsDefault = version.IsDefault,
            Description = a.Description,
            Documentation = a.Documentation,
            Icon = a.Icon,
            Labels = CloneLabels(a.Labels),
            CreatedAt = version.CreatedAt,
            ModifiedAt = version.ModifiedAt,
            ContentType = a.ContentType,
            Extensions = new Dictionary<string, byte[]>(a.Extensions),
        };
    }
}
