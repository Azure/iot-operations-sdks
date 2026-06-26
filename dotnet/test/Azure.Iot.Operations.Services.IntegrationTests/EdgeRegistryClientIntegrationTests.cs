// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.IntegrationTest;

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
/// The Edge Registry service ships separately from this SDK and is not yet part of the Azure IoT
/// Operations deployment that CI runs against. The service-dependent tests below are written, wired,
/// and ready to run: they are gated with <see cref="ServiceNotDeployedSkip"/> so the suite stays green
/// until the service is available. Once the service is reachable on the test broker, remove the
/// <c>Skip</c> argument from those facts (and adjust <see cref="GroupType"/>/<see cref="ResourceType"/>
/// to whatever Group/Resource types the deployed service accepts). The client-side guard tests at the
/// bottom do not depend on the service and run today against the broker alone.
/// </remarks>
[Trait("Category", "EdgeRegistry")]
public class EdgeRegistryClientIntegrationTests(ITestOutputHelper output)
{
    private const string ServiceNotDeployedSkip =
        "Requires the Edge Registry service, which is not yet deployed in Azure IoT Operations. " +
        "Remove the Skip once the service is reachable on the test broker.";

    // xRegistry collection names used to scope the test entities. These are the schema-extension
    // collection names, which a conformant xRegistry service exposes; adjust if the deployed service
    // accepts different Group/Resource types for the generic core surface.
    private const string GroupType = "schemagroups";
    private const string ResourceType = "schemas";

    [Fact(Skip = ServiceNotDeployedSkip)]
    public async Task CreateGetDeleteGroupRoundTrip()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using IEdgeRegistryClient client = new EdgeRegistryClient(applicationContext, mqttClient);

        string groupId = NewId("grp");

        // Create
        GroupEntity created = await client.CreateGroupAsync(GroupType, groupId, MakeGroupAttributes());
        output.WriteLine($"created group {created.Id} (xid {created.XId})");
        Assert.Equal(groupId, created.Id);
        Assert.Equal("integration-test group", created.Name);

        // Get
        GroupEntity fetched = await client.GetGroupAsync(GroupType, groupId);
        Assert.Equal(groupId, fetched.Id);
        Assert.Equal(created.XId, fetched.XId);

        // Delete (cascades to any contained Resources/Versions)
        await client.DeleteGroupAsync(GroupType, groupId);

        await client.StopAsync();
    }

    [Fact(Skip = ServiceNotDeployedSkip)]
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

    [Fact(Skip = ServiceNotDeployedSkip)]
    public async Task CreateResourceCreatesDefaultVersion()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using IEdgeRegistryClient client = new EdgeRegistryClient(applicationContext, mqttClient);

        string groupId = NewId("grp");
        string resourceId = NewId("res");

        try
        {
            ResourceEntity created = await client.CreateResourceAsync(
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
            ResourceEntity fetched = await client.GetResourceAsync(GroupType, groupId, ResourceType, resourceId);
            Assert.Equal(resourceId, fetched.Id);

            VersionEntity defaultVersion = await client.GetVersionAsync(
                GroupType, groupId, ResourceType, resourceId, GetVersionId.ResourceDefault);
            Assert.True(defaultVersion.IsDefault);
        }
        finally
        {
            await client.DeleteGroupAsync(GroupType, groupId);
        }

        await client.StopAsync();
    }

    [Fact(Skip = ServiceNotDeployedSkip)]
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
            VersionEntity second = await client.CreateVersionAsync(
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

    [Fact(Skip = ServiceNotDeployedSkip)]
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

    private static string NewId(string prefix) => $"it-{prefix}-{Guid.NewGuid():N}";

    private static GroupAttributes MakeGroupAttributes() => new()
    {
        Name = "integration-test group",
        Description = "created by EdgeRegistryClientIntegrationTests",
        Labels = [new Label { Key = "origin", Value = "integration-test" }],
        Extensions = new Dictionary<string, byte[]>(),
    };

    private static ResourceMetaAttributes MakeMeta() => new()
    {
        Labels = [new Label { Key = "origin", Value = "integration-test" }],
        Extensions = new Dictionary<string, byte[]>(),
    };

    private static VersionAttributes MakeVersionAttributes(string name) => new()
    {
        Name = name,
        Labels = [],
        ContentType = "application/json",
        Document = "{}"u8.ToArray(),
        Extensions = new Dictionary<string, byte[]>(),
    };
}
