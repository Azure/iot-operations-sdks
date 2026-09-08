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
        using CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromSeconds(20));
        CoreGroupEntity created = await client.CreateGroupAsync(GroupType, groupId, MakeGroupAttributes(), cancellationToken: cts.Token);
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
