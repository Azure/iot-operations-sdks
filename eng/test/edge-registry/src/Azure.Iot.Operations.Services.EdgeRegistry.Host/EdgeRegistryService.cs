// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Host;

using System.Globalization;
using System.Security.Cryptography;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Services.EdgeRegistry.Host.Generated;
using RegistryVersion = Azure.Iot.Operations.Services.EdgeRegistry.Host.Generated.Version;

/// <summary>
/// In-memory implementation of the core xRegistry surface (Group / Resource / Version) used by the
/// Edge Registry integration tests. It is a happy-path stub, not the production service: it keeps all
/// state in process memory, performs no validation or persistence, and exists so the SDK
/// <c>EdgeRegistryClient</c> has a server to exchange real MQTT RPC traffic with.
/// </summary>
internal sealed class EdgeRegistryService : EdgeRegistry.Service
{
    private const string DefaultGroupId = "default";
    private const string GroupTypeToken = "ex:groupType";
    private const string ResourceTypeToken = "ex:resourceType";
    private const string ResourceIdToken = "ex:resourceId";
    private const string VersionIdToken = "ex:versionId";

    private readonly object _gate = new();
    private readonly Dictionary<(string GroupType, string GroupId), StoredGroup> _groups = new();
    private readonly ILogger<EdgeRegistryService> _logger;

    public EdgeRegistryService(ApplicationContext applicationContext, MqttSessionClient mqttClient, ILogger<EdgeRegistryService> logger)
        : base(applicationContext, mqttClient)
    {
        _logger = logger;
    }

    // ---- Group APIs ----

    public override Task<ExtendedResponse<CreateGroupOutputArguments>> CreateGroupAsync(CreateGroupInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string groupType = TopicToken(requestMetadata, GroupTypeToken);
        string groupId = request.GroupId ?? DefaultGroupId;

        lock (_gate)
        {
            DateTime now = DateTime.UtcNow;
            if (!_groups.TryGetValue((groupType, groupId), out StoredGroup? group))
            {
                group = new StoredGroup(groupType, groupId, now);
                _groups[(groupType, groupId)] = group;
            }

            group.Name = request.Name;
            group.Description = request.Description;
            group.Documentation = request.Documentation;
            group.Icon = request.Icon;
            group.Labels = CloneLabels(request.Labels);
            group.Deprecated = request.Deprecated;
            group.Extensions = CloneExtensions(request.Extensions);
            group.ModifiedAt = now;
            group.Epoch++;

            _logger.LogInformation("CreateGroup {groupType}/{groupId}", groupType, groupId);
            return Task.FromResult(Ok(ToGroup(group)));
        }
    }

    public override Task<ExtendedResponse<GetGroupOutputArguments>> GetGroupAsync(GetGroupInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string groupType = TopicToken(requestMetadata, GroupTypeToken);
        string groupId = request.GroupId ?? DefaultGroupId;

        lock (_gate)
        {
            StoredGroup group = FindGroup(groupType, groupId);
            return Task.FromResult(Ok(ToGroup(group)));
        }
    }

    public override Task<ExtendedResponse<ListGroupsOutputArguments>> ListGroupsAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string groupType = TopicToken(requestMetadata, GroupTypeToken);

        lock (_gate)
        {
            List<string> ids = _groups.Values
                .Where(g => g.GroupType == groupType)
                .Select(g => g.GroupId)
                .ToList();
            return Task.FromResult(Ok(new ListGroupsOutputArguments { Ids = ids }));
        }
    }

    public override Task<ExtendedResponse<DeleteGroupOutputArguments>> DeleteGroupAsync(DeleteGroupInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string groupType = TopicToken(requestMetadata, GroupTypeToken);
        string groupId = request.GroupId ?? DefaultGroupId;

        lock (_gate)
        {
            _groups.Remove((groupType, groupId));
            return Task.FromResult(Ok(new DeleteGroupOutputArguments { DummyOutput = true }));
        }
    }

    // ---- Resource APIs ----

    public override Task<ExtendedResponse<CreateResourceOutputArguments>> CreateResourceAsync(CreateResourceInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string groupType = TopicToken(requestMetadata, GroupTypeToken);
        string resourceType = TopicToken(requestMetadata, ResourceTypeToken);
        string resourceId = TopicToken(requestMetadata, ResourceIdToken);
        string groupId = request.GroupId ?? DefaultGroupId;

        lock (_gate)
        {
            DateTime now = DateTime.UtcNow;
            StoredGroup group = GetOrCreateGroup(groupType, groupId, now);

            StoredResource resource = new(resourceType, resourceId, request.Meta, CloneExtensions(request.Extensions), now);
            string versionId = resource.AssignVersionId(request.DefaultVersionId);
            resource.Versions.Add(new StoredVersion(versionId, request.DefaultVersion, now));
            resource.DefaultVersionId = versionId;
            group.Resources[(resourceType, resourceId)] = resource;
            group.ModifiedAt = now;

            _logger.LogInformation("CreateResource {groupType}/{groupId}/{resourceType}/{resourceId}", groupType, groupId, resourceType, resourceId);
            return Task.FromResult(Ok(ToResource(group, resource)));
        }
    }

    public override Task<ExtendedResponse<GetResourceOutputArguments>> GetResourceAsync(GetResourceInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string groupType = TopicToken(requestMetadata, GroupTypeToken);
        string resourceType = TopicToken(requestMetadata, ResourceTypeToken);
        string resourceId = TopicToken(requestMetadata, ResourceIdToken);
        string groupId = request.GroupId ?? DefaultGroupId;

        lock (_gate)
        {
            StoredResource resource = FindResource(groupType, groupId, resourceType, resourceId, out StoredGroup group);
            return Task.FromResult(Ok(ToResource(group, resource)));
        }
    }

    public override Task<ExtendedResponse<ListResourcesOutputArguments>> ListResourcesAsync(ListResourcesInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            List<ResourceXid> resources = new();
            foreach (StoredGroup group in EnumerateGroups(request.GroupType, request.GroupId, request.AllGroups))
            {
                foreach (StoredResource resource in group.Resources.Values)
                {
                    if (request.ResourceType is not null && resource.ResourceType != request.ResourceType)
                    {
                        continue;
                    }

                    if (!HasLabel(resource.Meta.Labels, request.Label))
                    {
                        continue;
                    }

                    resources.Add(new ResourceXid
                    {
                        GroupType = group.GroupType,
                        GroupId = group.GroupId,
                        ResourceType = resource.ResourceType,
                        ResourceId = resource.ResourceId,
                    });
                }
            }

            return Task.FromResult(Ok(new ResourceXidList { Resources = resources }));
        }
    }

    public override Task<ExtendedResponse<DeleteResourceOutputArguments>> DeleteResourceAsync(DeleteResourceInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string groupType = TopicToken(requestMetadata, GroupTypeToken);
        string resourceType = TopicToken(requestMetadata, ResourceTypeToken);
        string resourceId = TopicToken(requestMetadata, ResourceIdToken);
        string groupId = request.GroupId ?? DefaultGroupId;

        lock (_gate)
        {
            if (_groups.TryGetValue((groupType, groupId), out StoredGroup? group))
            {
                group.Resources.Remove((resourceType, resourceId));
            }

            return Task.FromResult(Ok(new DeleteResourceOutputArguments { DummyOutput = true }));
        }
    }

    // ---- Version APIs ----

    public override Task<ExtendedResponse<CreateVersionOutputArguments>> CreateVersionAsync(CreateVersionInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string groupType = TopicToken(requestMetadata, GroupTypeToken);
        string resourceType = TopicToken(requestMetadata, ResourceTypeToken);
        string resourceId = TopicToken(requestMetadata, ResourceIdToken);
        string groupId = request.GroupId ?? DefaultGroupId;

        lock (_gate)
        {
            DateTime now = DateTime.UtcNow;
            StoredGroup group = GetOrCreateGroup(groupType, groupId, now);

            if (!group.Resources.TryGetValue((resourceType, resourceId), out StoredResource? resource))
            {
                // The parent Resource is implicitly created when it does not yet exist.
                ResourceMetaAttributes meta = new() { Labels = CloneLabels(request.ResourceLabels), Extensions = new Dictionary<string, byte[]>() };
                resource = new StoredResource(resourceType, resourceId, meta, new Dictionary<string, byte[]>(), now);
                group.Resources[(resourceType, resourceId)] = resource;
            }
            else
            {
                resource.Meta.Labels = CloneLabels(request.ResourceLabels);
                resource.ModifiedAt = now;
            }

            string versionId = resource.AssignVersionId(request.VersionId);
            StoredVersion version = new(versionId, request.Version, now);
            resource.Versions.Add(version);
            resource.DefaultVersionId = versionId; // newest Version becomes the default
            group.ModifiedAt = now;

            _logger.LogInformation("CreateVersion {groupType}/{groupId}/{resourceType}/{resourceId}/{versionId}", groupType, groupId, resourceType, resourceId, versionId);
            return Task.FromResult(Ok(ToVersion(group, resource, version)));
        }
    }

    public override Task<ExtendedResponse<GetVersionOutputArguments>> GetVersionAsync(GetVersionInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string groupType = TopicToken(requestMetadata, GroupTypeToken);
        string resourceType = TopicToken(requestMetadata, ResourceTypeToken);
        string resourceId = TopicToken(requestMetadata, ResourceIdToken);
        string groupId = request.GroupId ?? DefaultGroupId;

        lock (_gate)
        {
            StoredResource resource = FindResource(groupType, groupId, resourceType, resourceId, out StoredGroup group);
            string versionId = request.VersionId ?? resource.DefaultVersionId;
            StoredVersion version = resource.Versions.FirstOrDefault(v => v.VersionId == versionId)
                ?? throw NotFound($"/{groupType}/{groupId}/{resourceType}/{resourceId}/versions/{versionId}", "Version");
            return Task.FromResult(Ok(ToVersion(group, resource, version)));
        }
    }

    public override Task<ExtendedResponse<ListVersionsOutputArguments>> ListVersionsAsync(ListVersionsInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            List<VersionXid> versions = new();
            foreach (StoredGroup group in EnumerateGroups(request.GroupType, request.GroupId, request.AllGroups))
            {
                foreach (StoredResource resource in group.Resources.Values)
                {
                    if (request.ResourceType is not null && resource.ResourceType != request.ResourceType)
                    {
                        continue;
                    }

                    if (request.ResourceId is not null && resource.ResourceId != request.ResourceId)
                    {
                        continue;
                    }

                    foreach (StoredVersion version in resource.Versions)
                    {
                        if (!HasLabel(version.Attributes.Labels, request.Label))
                        {
                            continue;
                        }

                        versions.Add(new VersionXid
                        {
                            GroupType = group.GroupType,
                            GroupId = group.GroupId,
                            ResourceType = resource.ResourceType,
                            ResourceId = resource.ResourceId,
                            VersionId = version.VersionId,
                        });
                    }
                }
            }

            return Task.FromResult(Ok(new VersionXidList { Versions = versions }));
        }
    }

    public override Task<ExtendedResponse<DeleteVersionOutputArguments>> DeleteVersionAsync(DeleteVersionInputArguments request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        string groupType = TopicToken(requestMetadata, GroupTypeToken);
        string resourceType = TopicToken(requestMetadata, ResourceTypeToken);
        string resourceId = TopicToken(requestMetadata, ResourceIdToken);
        string versionId = TopicToken(requestMetadata, VersionIdToken);
        string groupId = request.GroupId ?? DefaultGroupId;

        lock (_gate)
        {
            if (_groups.TryGetValue((groupType, groupId), out StoredGroup? group)
                && group.Resources.TryGetValue((resourceType, resourceId), out StoredResource? resource))
            {
                resource.Versions.RemoveAll(v => v.VersionId == versionId);
                if (resource.DefaultVersionId == versionId)
                {
                    resource.DefaultVersionId = resource.Versions.Count > 0 ? resource.Versions[^1].VersionId : string.Empty;
                }
            }

            return Task.FromResult(Ok(new DeleteVersionOutputArguments { DummyOutput = true }));
        }
    }

    // ---- Lookup helpers ----

    private StoredGroup GetOrCreateGroup(string groupType, string groupId, DateTime now)
    {
        if (!_groups.TryGetValue((groupType, groupId), out StoredGroup? group))
        {
            group = new StoredGroup(groupType, groupId, now);
            _groups[(groupType, groupId)] = group;
        }

        return group;
    }

    private StoredGroup FindGroup(string groupType, string groupId)
        => _groups.TryGetValue((groupType, groupId), out StoredGroup? group)
            ? group
            : throw NotFound($"/{groupType}/{groupId}", "Group");

    private StoredResource FindResource(string groupType, string groupId, string resourceType, string resourceId, out StoredGroup group)
    {
        group = FindGroup(groupType, groupId);
        return group.Resources.TryGetValue((resourceType, resourceId), out StoredResource? resource)
            ? resource
            : throw NotFound($"/{groupType}/{groupId}/{resourceType}/{resourceId}", "Resource");
    }

    private IEnumerable<StoredGroup> EnumerateGroups(string? groupType, string? groupId, bool allGroups)
    {
        if (allGroups)
        {
            return _groups.Values;
        }

        string effectiveGroupId = groupId ?? DefaultGroupId;
        return _groups.Values.Where(g =>
            (groupType is null || g.GroupType == groupType) && g.GroupId == effectiveGroupId);
    }

    private static string TopicToken(CommandRequestMetadata metadata, string token)
        => metadata.TopicTokens.TryGetValue(token, out string? value)
            ? value
            : throw new InvalidOperationException($"Required topic token '{token}' was not present on the request.");

    private static EdgeRegistryErrorException NotFound(string subject, string kind)
        => new(new EdgeRegistryError
        {
            Code = 404,
            Status = "Not Found",
            Title = $"{kind} not found",
            Type = "about:blank",
            Subject = subject,
        });

    // ---- Wire mapping helpers ----

    private static ExtendedResponse<T> Ok<T>(T response)
        where T : class
        => new() { Response = response };

    private static Group ToGroup(StoredGroup group) => new()
    {
        Id = group.GroupId,
        Xid = $"/{group.GroupType}/{group.GroupId}",
        Epoch = group.Epoch,
        Name = group.Name,
        Description = group.Description,
        Documentation = group.Documentation,
        Icon = group.Icon,
        Labels = CloneLabels(group.Labels),
        CreatedAt = group.CreatedAt,
        ModifiedAt = group.ModifiedAt,
        Deprecated = group.Deprecated,
        ResourcesCounts = CountResources(group),
        Extensions = CloneExtensions(group.Extensions),
    };

    private static Resource ToResource(StoredGroup group, StoredResource resource) => new()
    {
        Id = resource.ResourceId,
        Xid = $"/{group.GroupType}/{group.GroupId}/{resource.ResourceType}/{resource.ResourceId}",
        Meta = ToResourceMeta(group, resource),
        DefaultVersion = ToVersion(group, resource, resource.DefaultVersion),
        VersionsCount = (ulong)resource.Versions.Count,
        Extensions = CloneExtensions(resource.Extensions),
    };

    private static ResourceMeta ToResourceMeta(StoredGroup group, StoredResource resource) => new()
    {
        Id = resource.ResourceId,
        Xid = $"/{group.GroupType}/{group.GroupId}/{resource.ResourceType}/{resource.ResourceId}/meta",
        Xref = resource.Meta.Xref,
        Epoch = resource.MetaEpoch,
        Labels = CloneLabels(resource.Meta.Labels),
        CreatedAt = resource.CreatedAt,
        ModifiedAt = resource.ModifiedAt,
        ReadOnly = false,
        Compatibility = resource.Meta.Compatibility,
        Deprecated = resource.Meta.Deprecated,
        DefaultVersionId = resource.DefaultVersionId,
        DefaultVersionSticky = false,
        Extensions = CloneExtensions(resource.Meta.Extensions),
    };

    private static RegistryVersion ToVersion(StoredGroup group, StoredResource resource, StoredVersion version)
    {
        VersionAttributes attributes = version.Attributes;
        return new RegistryVersion
        {
            ResourceId = resource.ResourceId,
            VersionId = version.VersionId,
            Xid = $"/{group.GroupType}/{group.GroupId}/{resource.ResourceType}/{resource.ResourceId}/versions/{version.VersionId}",
            Epoch = version.Epoch,
            Name = attributes.Name,
            IsDefault = resource.DefaultVersionId == version.VersionId,
            Description = attributes.Description,
            Documentation = attributes.Documentation,
            Icon = attributes.Icon,
            Labels = CloneLabels(attributes.Labels),
            CreatedAt = version.CreatedAt,
            ModifiedAt = version.ModifiedAt,
            Ancestor = attributes.Ancestor ?? version.VersionId,
            ContentType = attributes.ContentType,
            Format = attributes.Format,
            Document = attributes.Document,
            DocumentHash = attributes.Document is null ? null : Convert.ToHexString(SHA256.HashData(attributes.Document)),
            Extensions = CloneExtensions(attributes.Extensions),
        };
    }

    private static Dictionary<string, ulong> CountResources(StoredGroup group)
    {
        Dictionary<string, ulong> counts = new();
        foreach (StoredResource resource in group.Resources.Values)
        {
            counts[resource.ResourceType] = counts.GetValueOrDefault(resource.ResourceType) + 1;
        }

        return counts;
    }

    private static List<Label> CloneLabels(List<Label> labels)
        => labels.Select(l => new Label { Key = l.Key, Value = l.Value }).ToList();

    private static Dictionary<string, byte[]> CloneExtensions(Dictionary<string, byte[]> extensions)
        => new(extensions);

    private static bool HasLabel(List<Label> labels, Label? filter)
        => filter is null || labels.Any(l => l.Key == filter.Key && l.Value == filter.Value);

    // ---- In-memory storage ----

    private sealed class StoredGroup(string groupType, string groupId, DateTime createdAt)
    {
        public string GroupType { get; } = groupType;

        public string GroupId { get; } = groupId;

        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? Documentation { get; set; }

        public string? Icon { get; set; }

        public List<Label> Labels { get; set; } = new();

        public DeprecatedInfo? Deprecated { get; set; }

        public Dictionary<string, byte[]> Extensions { get; set; } = new();

        public DateTime CreatedAt { get; } = createdAt;

        public DateTime ModifiedAt { get; set; } = createdAt;

        public ulong Epoch { get; set; }

        public Dictionary<(string ResourceType, string ResourceId), StoredResource> Resources { get; } = new();
    }

    private sealed class StoredResource(string resourceType, string resourceId, ResourceMetaAttributes meta, Dictionary<string, byte[]> extensions, DateTime createdAt)
    {
        private int _nextVersionNumber = 1;

        public string ResourceType { get; } = resourceType;

        public string ResourceId { get; } = resourceId;

        public ResourceMetaAttributes Meta { get; } = meta;

        public Dictionary<string, byte[]> Extensions { get; } = extensions;

        public DateTime CreatedAt { get; } = createdAt;

        public DateTime ModifiedAt { get; set; } = createdAt;

        public ulong MetaEpoch { get; set; } = 1;

        public List<StoredVersion> Versions { get; } = new();

        public string DefaultVersionId { get; set; } = string.Empty;

        public StoredVersion DefaultVersion => Versions.First(v => v.VersionId == DefaultVersionId);

        public string AssignVersionId(string? requested)
        {
            if (!string.IsNullOrEmpty(requested))
            {
                return requested;
            }

            return (_nextVersionNumber++).ToString(CultureInfo.InvariantCulture);
        }
    }

    private sealed class StoredVersion(string versionId, VersionAttributes attributes, DateTime createdAt)
    {
        public string VersionId { get; } = versionId;

        public VersionAttributes Attributes { get; } = attributes;

        public DateTime CreatedAt { get; } = createdAt;

        public DateTime ModifiedAt { get; set; } = createdAt;

        public ulong Epoch { get; } = 1;
    }
}
