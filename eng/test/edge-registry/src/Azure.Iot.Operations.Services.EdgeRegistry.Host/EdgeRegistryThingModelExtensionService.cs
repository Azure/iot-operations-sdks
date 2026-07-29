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
/// In-memory implementation of the xRegistry Thing Model extension surface used by the integration tests.
/// </summary>
internal sealed class EdgeRegistryThingModelExtensionService : EdgeRegistryThingModelExtensions.Service
{
    private const string GroupType = "thingmodelgroups";
    private const string ResourceType = "thingmodels";
    private const string ThingModelIdToken = "ex:thingModelId";

    private readonly ExtensionVersionStore<CreateThingModelVersionAttributes> _store = new(a => a.Labels, a => a.Document);

    public EdgeRegistryThingModelExtensionService(ApplicationContext applicationContext, MqttSessionClient mqttClient)
        : base(applicationContext, mqttClient)
    {
    }

    public override Task<ExtendedResponse<CreateThingModelVersionOutputArguments>> CreateThingModelVersionAsync(CreateThingModelVersionInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string thingModelId = TopicToken(requestMetadata, ThingModelIdToken);
        string groupId = request.GroupId ?? DefaultGroupId;
        var version = _store.CreateVersion(groupId, thingModelId, request.ThingModelLabels, request);
        return Task.FromResult(Ok(ToEntity(groupId, thingModelId, version)));
    }

    public override Task<ExtendedResponse<GetThingModelVersionOutputArguments>> GetThingModelVersionAsync(GetThingModelVersionInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string thingModelId = TopicToken(requestMetadata, ThingModelIdToken);
        string groupId = request.GroupId ?? DefaultGroupId;
        var version = _store.GetVersion(groupId, thingModelId, request.VersionId)
            ?? throw new ThingModelExtensionErrorException(new ThingModelExtensionError
            {
                Code = 404,
                Status = "Not Found",
                Title = "Thing Model Version not found",
                Type = "about:blank",
            });
        return Task.FromResult(Ok(ToEntity(groupId, thingModelId, version)));
    }

    public override Task<ExtendedResponse<ListThingModelVersionsOutputArguments>> ListThingModelVersionsAsync(ListThingModelVersionsInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string? groupId = request.AllGroups ? null : (request.GroupId ?? DefaultGroupId);
        List<ThingModelVersionXid> versions = _store
            .ListVersions(groupId, request.AllGroups, request.ResourceId, request.DocumentHash, request.Label)
            .Select(t => new ThingModelVersionXid
            {
                GroupType = GroupType,
                GroupId = t.GroupId,
                ResourceType = ResourceType,
                ResourceId = t.ResourceId,
                VersionId = t.VersionId,
            })
            .ToList();
        return Task.FromResult(Ok(new ThingModelVersionXidList { Versions = versions }));
    }

    public override Task<ExtendedResponse<EmptyJson>> DeleteThingModelVersionAsync(DeleteThingModelVersionInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string thingModelId = TopicToken(requestMetadata, ThingModelIdToken);
        ulong versionId = ulong.Parse(TopicToken(requestMetadata, VersionIdToken), CultureInfo.InvariantCulture);
        string groupId = request.GroupId ?? DefaultGroupId;
        _store.DeleteVersion(groupId, thingModelId, versionId, request.Options.ExpectedEpoch);
        return Task.FromResult(Ok(new EmptyJson()));
    }

    private static ThingModelVersion ToEntity(string groupId, string thingModelId, ExtensionVersionStore<CreateThingModelVersionAttributes>.StoredVersion version)
    {
        CreateThingModelVersionAttributes a = version.Attributes;
        return new ThingModelVersion
        {
            VersionId = version.VersionId,
            Ancestor = a.Ancestor ?? version.VersionId,
            Format = a.Format,
            Document = a.Document,
            DocumentHash = ComputeHash(a.Document),
            ResourceId = thingModelId,
            Xid = $"/{GroupType}/{groupId}/{ResourceType}/{thingModelId}/versions/{version.VersionId}",
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
