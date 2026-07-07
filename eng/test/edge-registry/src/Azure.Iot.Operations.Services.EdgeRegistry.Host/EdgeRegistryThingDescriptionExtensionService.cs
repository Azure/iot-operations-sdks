// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Host;

using System.Globalization;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Services.EdgeRegistry.Host.Generated;
using static Azure.Iot.Operations.Services.EdgeRegistry.Host.ExtensionStub;

/// <summary>
/// In-memory implementation of the xRegistry Thing Description extension surface used by the integration tests.
/// </summary>
internal sealed class EdgeRegistryThingDescriptionExtensionService : EdgeRegistryThingDescriptionExtensions.Service
{
    private const string GroupType = "thingdescriptiongroups";
    private const string ResourceType = "thingdescriptions";
    private const string ThingDescriptionIdToken = "ex:thingDescriptionId";

    private readonly ExtensionVersionStore<CreateThingDescriptionVersionAttributes> _store = new(a => a.Labels);

    public EdgeRegistryThingDescriptionExtensionService(ApplicationContext applicationContext, MqttSessionClient mqttClient)
        : base(applicationContext, mqttClient)
    {
    }

    public override Task<ExtendedResponse<CreateThingDescriptionVersionOutputArguments>> CreateThingDescriptionVersionAsync(CreateThingDescriptionVersionInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string thingDescriptionId = TopicToken(requestMetadata, ThingDescriptionIdToken);
        string groupId = request.GroupId ?? DefaultGroupId;
        var version = _store.CreateVersion(groupId, thingDescriptionId, request.ThingDescriptionLabels, request);
        return Task.FromResult(Ok(ToEntity(groupId, thingDescriptionId, version)));
    }

    public override Task<ExtendedResponse<GetThingDescriptionVersionOutputArguments>> GetThingDescriptionVersionAsync(GetThingDescriptionVersionInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string thingDescriptionId = TopicToken(requestMetadata, ThingDescriptionIdToken);
        string groupId = request.GroupId ?? DefaultGroupId;
        var version = _store.GetVersion(groupId, thingDescriptionId, request.VersionId)
            ?? throw new ThingDescriptionExtensionErrorException(new ThingDescriptionExtensionError
            {
                Code = 404,
                Status = "Not Found",
                Title = "Thing Description Version not found",
                Type = "about:blank",
            });
        return Task.FromResult(Ok(ToEntity(groupId, thingDescriptionId, version)));
    }

    public override Task<ExtendedResponse<ListThingDescriptionVersionsOutputArguments>> ListThingDescriptionVersionsAsync(ListThingDescriptionVersionsInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string? groupId = request.AllGroups ? null : (request.GroupId ?? DefaultGroupId);
        List<ThingDescriptionVersionXid> versions = _store
            .ListVersions(groupId, request.AllGroups, request.ResourceId, request.Label)
            .Select(t => new ThingDescriptionVersionXid
            {
                GroupType = GroupType,
                GroupId = t.GroupId,
                ResourceType = ResourceType,
                ResourceId = t.ResourceId,
                VersionId = t.VersionId,
            })
            .ToList();
        return Task.FromResult(Ok(new ThingDescriptionVersionXidList { Versions = versions }));
    }

    public override Task<ExtendedResponse<DeleteThingDescriptionVersionOutputArguments>> DeleteThingDescriptionVersionAsync(DeleteThingDescriptionVersionInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string thingDescriptionId = TopicToken(requestMetadata, ThingDescriptionIdToken);
        ulong versionId = ulong.Parse(TopicToken(requestMetadata, VersionIdToken), CultureInfo.InvariantCulture);
        string groupId = request.GroupId ?? DefaultGroupId;
        _store.DeleteVersion(groupId, thingDescriptionId, versionId);
        return Task.FromResult(Ok(new DeleteThingDescriptionVersionOutputArguments { DummyOutput = true }));
    }

    private static ThingDescriptionVersion ToEntity(string groupId, string thingDescriptionId, ExtensionVersionStore<CreateThingDescriptionVersionAttributes>.StoredVersion version)
    {
        CreateThingDescriptionVersionAttributes a = version.Attributes;
        return new ThingDescriptionVersion
        {
            VersionId = version.VersionId,
            Ancestor = a.Ancestor ?? version.VersionId,
            Format = a.Format,
            Document = a.Document,
            DocumentHash = ComputeHash(a.Document),
            ResourceId = thingDescriptionId,
            Xid = $"/{GroupType}/{groupId}/{ResourceType}/{thingDescriptionId}/versions/{version.VersionId}",
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
