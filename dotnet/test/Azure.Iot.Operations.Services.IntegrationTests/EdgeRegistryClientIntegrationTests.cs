// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.IntegrationTest;

using System.Globalization;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.EdgeRegistry;
using Azure.Iot.Operations.Services.EdgeRegistry.Models;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Integration tests for <see cref="EdgeRegistryClient"/> exercising the core xRegistry surface
/// (Group, Resource, and Version CRUD plus listing) against a live Edge Registry service.
/// </summary>
/// <remarks>
/// These tests run against the in-process Edge Registry stub host under <c>eng/test/edge-registry</c>,
/// which CI starts (alongside the SchemaRegistry host) before the test run. The stub implements the
/// core xRegistry surface in memory, so the assertions below exercise the real MQTT RPC round trip:
/// topic routing, <c>ex:</c> token prefixing, payload serialization, and the client's wire-to-model
/// mapping. The client-side guard tests at the bottom do not depend on the host.
/// </remarks>
[Trait("Category", "EdgeRegistry")]
public class EdgeRegistryClientIntegrationTests(ITestOutputHelper output)
{
    // xRegistry collection names used to scope the test entities. These are the schema-extension
    // collection names; the stub host accepts any Group/Resource type, so adjust these only if the
    // tests are pointed at a different Edge Registry service.
    private const string GroupType = "schemagroups";
    private const string ResourceType = "schemas";

    // Display name applied to Groups created by these tests. Factored out because it is both set on
    // the created Group (see MakeGroupAttributes) and asserted on the round-tripped Group.
    private const string GroupName = "integration-test group";

    [Fact]
    public async Task CreateGetDeleteGroupRoundTrip()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using IEdgeRegistryClient client = new EdgeRegistryClient(applicationContext, mqttClient);

        string groupId = NewId("grp");

        // Create
        CoreGroupEntity created = await client.CreateGroupAsync(GroupType, groupId, MakeGroupAttributes());
        output.WriteLine($"created group {created.Id} (xid {created.XId})");
        Assert.Equal(groupId, created.Id);
        Assert.Equal(GroupName, created.Name);

        // Get
        CoreGroupEntity fetched = await client.GetGroupAsync(GroupType, groupId);
        Assert.Equal(groupId, fetched.Id);
        Assert.Equal(created.XId, fetched.XId);

        // Delete (cascades to any contained Resources/Versions)
        await client.DeleteGroupAsync(GroupType, groupId);

        await client.StopAsync();
    }

    [Fact]
    public async Task ListGroupsIncludesCreatedGroup()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using IEdgeRegistryClient client = new EdgeRegistryClient(applicationContext, mqttClient);

        string groupId = NewId("grp");
        await client.CreateGroupAsync(GroupType, groupId, MakeGroupAttributes());

        try
        {
            IReadOnlyList<string> groups = await client.ListGroupsAsync(GroupType);
            Assert.Contains(groupId, groups);
        }
        finally
        {
            await client.DeleteGroupAsync(GroupType, groupId);
        }

        await client.StopAsync();
    }

    [Fact]
    public async Task CreateResourceCreatesDefaultVersion()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using IEdgeRegistryClient client = new EdgeRegistryClient(applicationContext, mqttClient);

        string groupId = NewId("grp");
        string resourceId = NewId("res");

        try
        {
            CoreResourceEntity created = await client.CreateResourceAsync(
                GroupType,
                groupId,
                ResourceType,
                resourceId,
                MakeMeta(),
                new Dictionary<string, byte[]>(),
                CreateVersionId.ServerAssigned,
                MakeVersionAttributes("v1"));

            output.WriteLine($"created resource {created.Id} with default version {created.DefaultVersion.VersionId}");
            Assert.Equal(resourceId, created.Id);
            Assert.True(created.DefaultVersion.IsDefault);
            Assert.True(created.VersionsCount >= 1);

            // The Resource and its default Version are retrievable.
            CoreResourceEntity fetched = await client.GetResourceAsync(GroupType, groupId, ResourceType, resourceId);
            Assert.Equal(resourceId, fetched.Id);

            CoreVersionEntity defaultVersion = await client.GetVersionAsync(
                GroupType, groupId, ResourceType, resourceId, GetVersionId.ResourceDefault);
            Assert.True(defaultVersion.IsDefault);
        }
        finally
        {
            await client.DeleteGroupAsync(GroupType, groupId);
        }

        await client.StopAsync();
    }

    [Fact]
    public async Task CreateAndListVersionsRoundTrip()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using IEdgeRegistryClient client = new EdgeRegistryClient(applicationContext, mqttClient);

        string groupId = NewId("grp");
        string resourceId = NewId("res");

        try
        {
            // Seed the Resource with its first (default) Version.
            await client.CreateResourceAsync(
                GroupType,
                groupId,
                ResourceType,
                resourceId,
                MakeMeta(),
                new Dictionary<string, byte[]>(),
                CreateVersionId.ServerAssigned,
                MakeVersionAttributes("v1"));

            // Add a second Version; the newest Version becomes the Resource default.
            CoreVersionEntity second = await client.CreateVersionAsync(
                GroupType,
                groupId,
                ResourceType,
                resourceId,
                resourceLabels: [],
                CreateVersionId.ServerAssigned,
                MakeVersionAttributes("v2"));
            Assert.True(second.IsDefault);

            IReadOnlyList<VersionXId> versions = await client.ListVersionsAsync(
                GroupQuery.WithinGroupType(GroupType, GroupSelector.Specific(groupId)),
                resourceType: ResourceType,
                resourceId: resourceId);

            Assert.True(versions.Count >= 2);
            Assert.All(versions, v => Assert.Equal(resourceId, v.ResourceId));

            // Drop the just-created Version, leaving the Resource intact.
            await client.DeleteVersionAsync(GroupType, groupId, ResourceType, resourceId, second.VersionId);
        }
        finally
        {
            await client.DeleteGroupAsync(GroupType, groupId);
        }

        await client.StopAsync();
    }

    [Fact]
    public async Task ListResourcesIncludesCreatedResource()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using IEdgeRegistryClient client = new EdgeRegistryClient(applicationContext, mqttClient);

        string groupId = NewId("grp");
        string resourceId = NewId("res");

        try
        {
            await client.CreateResourceAsync(
                GroupType,
                groupId,
                ResourceType,
                resourceId,
                MakeMeta(),
                new Dictionary<string, byte[]>(),
                CreateVersionId.ServerAssigned,
                MakeVersionAttributes("v1"));

            IReadOnlyList<ResourceXId> resources = await client.ListResourcesAsync(
                GroupQuery.WithinGroupType(GroupType, GroupSelector.Specific(groupId)),
                resourceType: ResourceType);

            Assert.Contains(resources, r => r.ResourceId == resourceId);
        }
        finally
        {
            await client.DeleteGroupAsync(GroupType, groupId);
        }

        await client.StopAsync();
    }

    [Fact]
    public async Task ListResourcesAllGroupsStaysWithinGroupType()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using IEdgeRegistryClient client = new EdgeRegistryClient(applicationContext, mqttClient);

        const string otherGroupType = "othergroups";
        string groupId = NewId("grp");
        string otherGroupId = NewId("grp");
        string resourceId = NewId("res");
        string otherResourceId = NewId("res");

        try
        {
            await client.CreateResourceAsync(GroupType, groupId, ResourceType, resourceId, MakeMeta(), [], CreateVersionId.ServerAssigned, MakeVersionAttributes("v1"));
            await client.CreateResourceAsync(otherGroupType, otherGroupId, ResourceType, otherResourceId, MakeMeta(), [], CreateVersionId.ServerAssigned, MakeVersionAttributes("v1"));

            IReadOnlyList<ResourceXId> resources = await client.ListResourcesAsync(
                GroupQuery.WithinGroupType(GroupType, GroupSelector.All),
                resourceType: ResourceType);

            Assert.Contains(resources, resource => resource.ResourceId == resourceId);
            Assert.DoesNotContain(resources, resource => resource.ResourceId == otherResourceId);
        }
        finally
        {
            await client.DeleteGroupAsync(GroupType, groupId);
            await client.DeleteGroupAsync(otherGroupType, otherGroupId);
        }

        await client.StopAsync();
    }

    [Fact]
    public async Task ThrowsObjectDisposedExceptionWhenDisposed()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        IEdgeRegistryClient client = new EdgeRegistryClient(applicationContext, mqttClient);

        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await client.ListGroupsAsync(GroupType));
    }

    [Fact]
    public async Task ThrowsOperationCanceledExceptionWhenCancellationRequested()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using IEdgeRegistryClient client = new EdgeRegistryClient(applicationContext, mqttClient);

        using CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await client.ListGroupsAsync(GroupType, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task SchemaVersionRoundTrip()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using IEdgeRegistryClient client = new EdgeRegistryClient(applicationContext, mqttClient);

        string schemaId = NewId("schema");
        Label schemaLabel = MakeResourceLabel(schemaId);

        SchemaVersion created = await client.CreateSchemaVersionAsync(GroupId.CloudDefault, schemaId, [schemaLabel], MakeSchemaVersionAttributes());
        output.WriteLine($"created schema version {created.VersionId} for {created.ResourceId}");
        Assert.Equal(schemaId, created.ResourceId);
        Assert.True(created.IsDefault);

        SchemaVersion fetched = await client.GetSchemaVersionAsync(GroupId.CloudDefault, schemaId, GetSchemaVersionId.ResourceDefault);
        Assert.Equal(created.VersionId, fetched.VersionId);

        // The newest Version becomes the Schema's default.
        SchemaVersion second = await client.CreateSchemaVersionAsync(GroupId.CloudDefault, schemaId, [schemaLabel], MakeSchemaVersionAttributes());
        Assert.True(second.IsDefault);
        Assert.NotEqual(created.VersionId, second.VersionId);

        CoreResourceEntity coreSchema = await client.GetResourceAsync(GroupType, GroupId.CloudDefault, ResourceType, schemaId);
        Assert.Equal(second.VersionId.ToString(CultureInfo.InvariantCulture), coreSchema.DefaultVersion.VersionId);

        IReadOnlyList<VersionXId> coreVersions = await client.ListVersionsAsync(
            GroupQuery.WithinGroupType(GroupType, GroupSelector.Default),
            resourceType: ResourceType,
            resourceId: schemaId);
        Assert.Contains(coreVersions, version => version.VersionId == created.VersionId.ToString(CultureInfo.InvariantCulture));
        Assert.Contains(coreVersions, version => version.VersionId == second.VersionId.ToString(CultureInfo.InvariantCulture));

        IReadOnlyList<SchemaVersionXid> versions = await client.ListSchemaVersionsAsync(GroupSelector.Default, schemaId);
        Assert.True(versions.Count >= 2);
        Assert.All(versions, v => Assert.Equal(schemaId, v.ResourceId));

        IReadOnlyList<ResourceXId> schemas = await client.ListSchemasAsync(GroupSelector.Default, schemaLabel);
        ResourceXId schema = Assert.Single(schemas);
        Assert.Equal(schemaId, schema.ResourceId);

        await client.DeleteSchemaVersionAsync(GroupId.CloudDefault, schemaId, second.VersionId);

        coreSchema = await client.GetResourceAsync(GroupType, GroupId.CloudDefault, ResourceType, schemaId);
        Assert.Equal(created.VersionId.ToString(CultureInfo.InvariantCulture), coreSchema.DefaultVersion.VersionId);

        await client.StopAsync();
    }

    [Fact]
    public async Task SchemaListSelectorsMapAllAndSpecificGroups()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using IEdgeRegistryClient client = new EdgeRegistryClient(applicationContext, mqttClient);

        string firstGroupId = NewId("grp");
        string secondGroupId = NewId("grp");
        string firstSchemaId = NewId("schema");
        string secondSchemaId = NewId("schema");
        Label selectorLabel = MakeResourceLabel(NewId("selector"));
        SchemaVersion? firstVersion = null;
        SchemaVersion? secondVersion = null;

        try
        {
            firstVersion = await client.CreateSchemaVersionAsync(GroupId.Specific(firstGroupId), firstSchemaId, [selectorLabel], MakeSchemaVersionAttributes());
            secondVersion = await client.CreateSchemaVersionAsync(GroupId.Specific(secondGroupId), secondSchemaId, [selectorLabel], MakeSchemaVersionAttributes());

            IReadOnlyList<ResourceXId> allSchemas = await client.ListSchemasAsync(GroupSelector.All, selectorLabel);
            Assert.Contains(allSchemas, schema => schema.ResourceId == firstSchemaId);
            Assert.Contains(allSchemas, schema => schema.ResourceId == secondSchemaId);

            IReadOnlyList<ResourceXId> firstGroupSchemas = await client.ListSchemasAsync(GroupSelector.Specific(firstGroupId), selectorLabel);
            Assert.Contains(firstGroupSchemas, schema => schema.ResourceId == firstSchemaId);
            Assert.DoesNotContain(firstGroupSchemas, schema => schema.ResourceId == secondSchemaId);

            IReadOnlyList<SchemaVersionXid> allVersions = await client.ListSchemaVersionsAsync(GroupSelector.All);
            Assert.Contains(allVersions, version => version.ResourceId == firstSchemaId && version.VersionId == firstVersion.VersionId);
            Assert.Contains(allVersions, version => version.ResourceId == secondSchemaId && version.VersionId == secondVersion.VersionId);

            IReadOnlyList<SchemaVersionXid> firstGroupVersions = await client.ListSchemaVersionsAsync(GroupSelector.Specific(firstGroupId));
            Assert.Contains(firstGroupVersions, version => version.ResourceId == firstSchemaId);
            Assert.DoesNotContain(firstGroupVersions, version => version.ResourceId == secondSchemaId);
        }
        finally
        {
            if (firstVersion is not null)
            {
                await client.DeleteSchemaVersionAsync(GroupId.Specific(firstGroupId), firstSchemaId, firstVersion.VersionId);
            }

            if (secondVersion is not null)
            {
                await client.DeleteSchemaVersionAsync(GroupId.Specific(secondGroupId), secondSchemaId, secondVersion.VersionId);
            }

            await client.DeleteGroupAsync(GroupType, firstGroupId);
            await client.DeleteGroupAsync(GroupType, secondGroupId);
        }

        await client.StopAsync();
    }

    [Fact]
    public async Task ThingDescriptionVersionRoundTrip()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using IEdgeRegistryClient client = new EdgeRegistryClient(applicationContext, mqttClient);

        string thingDescriptionId = NewId("td");
        Label thingDescriptionLabel = MakeResourceLabel(thingDescriptionId);

        ThingDescriptionVersion created = await client.CreateThingDescriptionVersionAsync(GroupId.CloudDefault, thingDescriptionId, [thingDescriptionLabel], MakeThingDescriptionVersionAttributes());
        Assert.Equal(thingDescriptionId, created.ResourceId);
        Assert.True(created.IsDefault);

        ThingDescriptionVersion fetched = await client.GetThingDescriptionVersionAsync(GroupId.CloudDefault, thingDescriptionId, GetThingDescriptionVersionId.ResourceDefault);
        Assert.Equal(created.VersionId, fetched.VersionId);

        ThingDescriptionVersion second = await client.CreateThingDescriptionVersionAsync(GroupId.CloudDefault, thingDescriptionId, [thingDescriptionLabel], MakeThingDescriptionVersionAttributes());
        Assert.True(second.IsDefault);

        IReadOnlyList<ThingDescriptionVersionXid> versions = await client.ListThingDescriptionVersionsAsync(GroupSelector.Default, thingDescriptionId);
        Assert.True(versions.Count >= 2);
        Assert.All(versions, v => Assert.Equal(thingDescriptionId, v.ResourceId));

        IReadOnlyList<ResourceXId> thingDescriptions = await client.ListThingDescriptionsAsync(GroupSelector.Default, thingDescriptionLabel);
        ResourceXId thingDescription = Assert.Single(thingDescriptions);
        Assert.Equal(thingDescriptionId, thingDescription.ResourceId);

        await client.DeleteThingDescriptionVersionAsync(GroupId.CloudDefault, thingDescriptionId, second.VersionId);

        await client.StopAsync();
    }

    [Fact]
    public async Task ThingModelVersionRoundTrip()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using IEdgeRegistryClient client = new EdgeRegistryClient(applicationContext, mqttClient);

        string thingModelId = NewId("tm");
        Label thingModelLabel = MakeResourceLabel(thingModelId);

        ThingModelVersion created = await client.CreateThingModelVersionAsync(GroupId.CloudDefault, thingModelId, [thingModelLabel], MakeThingModelVersionAttributes());
        Assert.Equal(thingModelId, created.ResourceId);
        Assert.True(created.IsDefault);

        ThingModelVersion fetched = await client.GetThingModelVersionAsync(GroupId.CloudDefault, thingModelId, GetThingModelVersionId.ResourceDefault);
        Assert.Equal(created.VersionId, fetched.VersionId);

        ThingModelVersion second = await client.CreateThingModelVersionAsync(GroupId.CloudDefault, thingModelId, [thingModelLabel], MakeThingModelVersionAttributes());
        Assert.True(second.IsDefault);

        IReadOnlyList<ThingModelVersionXid> versions = await client.ListThingModelVersionsAsync(GroupSelector.Default, thingModelId);
        Assert.True(versions.Count >= 2);
        Assert.All(versions, v => Assert.Equal(thingModelId, v.ResourceId));

        IReadOnlyList<ResourceXId> thingModels = await client.ListThingModelsAsync(GroupSelector.Default, thingModelLabel);
        ResourceXId thingModel = Assert.Single(thingModels);
        Assert.Equal(thingModelId, thingModel.ResourceId);

        await client.DeleteThingModelVersionAsync(GroupId.CloudDefault, thingModelId, second.VersionId);

        await client.StopAsync();
    }

    private static string NewId(string prefix) => $"it-{prefix}-{Guid.NewGuid():N}";

    private static Label MakeResourceLabel(string value) => new() { Key = "managed-by", Value = value };

    private static CoreGroupAttributes MakeGroupAttributes() => new()
    {
        Name = GroupName,
        Description = "created by EdgeRegistryClientIntegrationTests",
        Labels = [new Label { Key = "origin", Value = "integration-test" }],
        Extensions = new Dictionary<string, byte[]>(),
    };

    private static CoreResourceMetaAttributes MakeMeta() => new()
    {
        Labels = [new Label { Key = "origin", Value = "integration-test" }],
        Extensions = new Dictionary<string, byte[]>(),
    };

    private static CoreVersionAttributes MakeVersionAttributes(string name) => new()
    {
        Name = name,
        Labels = [],
        ContentType = "application/json",
        Document = "{}"u8.ToArray(),
        Extensions = new Dictionary<string, byte[]>(),
    };

    private static SchemaVersionAttributes MakeSchemaVersionAttributes() => new()
    {
        Labels = [],
        Format = SchemaFormat.JsonSchemaDraft07,
        Document = "{}"u8.ToArray(),
        Extensions = new Dictionary<string, byte[]>(),
    };

    private static ThingDescriptionVersionAttributes MakeThingDescriptionVersionAttributes() => new()
    {
        Labels = [],
        Format = ThingDescriptionFormat.JsonLd11,
        Document = "{}"u8.ToArray(),
        Extensions = new Dictionary<string, byte[]>(),
    };

    private static ThingModelVersionAttributes MakeThingModelVersionAttributes() => new()
    {
        Labels = [],
        Format = ThingModelFormat.JsonLd11,
        Document = "{}"u8.ToArray(),
        Extensions = new Dictionary<string, byte[]>(),
    };
}
