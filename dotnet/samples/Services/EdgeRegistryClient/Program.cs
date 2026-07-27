// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Services.EdgeRegistry;
using Azure.Iot.Operations.Services.EdgeRegistry.Models;

using Microsoft.Extensions.Configuration;

// Sample documents. The second revision of each Resource differs from the first.
const string SchemaV1 = """{"$schema":"http://json-schema.org/draft-07/schema#","type":"object","properties":{"temperature":{"type":"number"}}}""";
const string SchemaV2 = """{"$schema":"http://json-schema.org/draft-07/schema#","type":"object","properties":{"temperature":{"type":"number"},"humidity":{"type":"number"}}}""";
const string ThingDescriptionV1 = """{"@context":"https://www.w3.org/2022/wot/td/v1.1","title":"Thermostat","properties":{"temperature":{"type":"number"}}}""";
const string ThingDescriptionV2 = """{"@context":"https://www.w3.org/2022/wot/td/v1.1","title":"Thermostat","properties":{"temperature":{"type":"number"},"humidity":{"type":"number"}}}""";
const string ThingModelV1 = """{"@context":"https://www.w3.org/2022/wot/td/v1.1","@type":"tm:ThingModel","title":"Thermostat","properties":{"temperature":{"type":"number"}}}""";
const string ThingModelV2 = """{"@context":"https://www.w3.org/2022/wot/td/v1.1","@type":"tm:ThingModel","title":"Thermostat","properties":{"temperature":{"type":"number"},"humidity":{"type":"number"}}}""";

IConfigurationRoot configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

bool mqttDiag = Convert.ToBoolean(configuration["mqttDiag"]);
if (mqttDiag)
{
    Trace.Listeners.Add(new ConsoleTraceListener());
}

MqttSessionClient mqttClient = new(new MqttSessionClientOptions { EnableMqttLogging = mqttDiag });
ApplicationContext applicationContext = new();
await using EdgeRegistryClient client = new(applicationContext, mqttClient);
await mqttClient.ConnectAsync(MqttConnectionSettings.FromEnvVars());

await SchemaDemo(client);
await ThingDescriptionDemo(client);
await ThingModelDemo(client);

await client.StopAsync();

// Builds a single label key/value pair.
static Label MakeLabel(string key, string value) => new() { Key = key, Value = value };

// Schema extension: create -> get -> create -> list.
static async Task SchemaDemo(IEdgeRegistryClient client)
{
    const string schemaId = "sample-schema";
    Console.WriteLine("--- Schema extension ---");

    // Create the first Version (implicitly creating the Schema Resource), labeling both the
    // Resource and the Version.
    SchemaVersion first = await client.CreateSchemaVersionAsync(
        GroupId.CloudDefault,
        schemaId,
        new List<Label> { MakeLabel("managed-by", "sample") },
        new SchemaVersionAttributes
        {
            Format = SchemaFormat.JsonSchemaDraft07,
            Document = Encoding.UTF8.GetBytes(SchemaV1),
            Labels = new List<Label> { MakeLabel("revision", "1") },
            Extensions = new(),
        });
    Console.WriteLine($"Created Schema Version {first.VersionId} (xid {first.XId})");

    // Get the Version that was just created.
    SchemaVersion got = await client.GetSchemaVersionAsync(
        GroupId.CloudDefault,
        schemaId,
        GetSchemaVersionId.Specific(first.VersionId));
    Console.WriteLine($"Got Schema Version {got.VersionId} (isDefault: {got.IsDefault})");

    // Create a second, different Version of the same Schema.
    SchemaVersion second = await client.CreateSchemaVersionAsync(
        GroupId.CloudDefault,
        schemaId,
        new List<Label> { MakeLabel("managed-by", "sample") },
        new SchemaVersionAttributes
        {
            Format = SchemaFormat.JsonSchemaDraft07,
            Document = Encoding.UTF8.GetBytes(SchemaV2),
            Labels = new List<Label> { MakeLabel("revision", "2") },
            Extensions = new(),
        });
    Console.WriteLine($"Created Schema Version {second.VersionId}");

    // List the Versions of this Schema (now two).
    IReadOnlyList<SchemaVersionXid> versions = await client.ListSchemaVersionsAsync(GroupSelector.Default, schemaId);
    Console.WriteLine($"Listed {versions.Count} Schema Version(s): {string.Join(", ", versions.Select(v => v.VersionId))}");
}

// Thing Description extension: create -> get -> create -> list.
static async Task ThingDescriptionDemo(IEdgeRegistryClient client)
{
    const string thingDescriptionId = "sample-thing-description";
    Console.WriteLine("--- Thing Description extension ---");

    ThingDescriptionVersion first = await client.CreateThingDescriptionVersionAsync(
        GroupId.CloudDefault,
        thingDescriptionId,
        new List<Label> { MakeLabel("managed-by", "sample") },
        new ThingDescriptionVersionAttributes
        {
            Format = ThingDescriptionFormat.JsonLd11,
            Document = Encoding.UTF8.GetBytes(ThingDescriptionV1),
            Labels = new List<Label> { MakeLabel("revision", "1") },
            Extensions = new(),
        });
    Console.WriteLine($"Created Thing Description Version {first.VersionId} (xid {first.XId})");

    ThingDescriptionVersion got = await client.GetThingDescriptionVersionAsync(
        GroupId.CloudDefault,
        thingDescriptionId,
        GetThingDescriptionVersionId.Specific(first.VersionId));
    Console.WriteLine($"Got Thing Description Version {got.VersionId} (isDefault: {got.IsDefault})");

    ThingDescriptionVersion second = await client.CreateThingDescriptionVersionAsync(
        GroupId.CloudDefault,
        thingDescriptionId,
        new List<Label> { MakeLabel("managed-by", "sample") },
        new ThingDescriptionVersionAttributes
        {
            Format = ThingDescriptionFormat.JsonLd11,
            Document = Encoding.UTF8.GetBytes(ThingDescriptionV2),
            Labels = new List<Label> { MakeLabel("revision", "2") },
            Extensions = new(),
        });
    Console.WriteLine($"Created Thing Description Version {second.VersionId}");

    IReadOnlyList<ThingDescriptionVersionXid> versions = await client.ListThingDescriptionVersionsAsync(GroupSelector.Default, thingDescriptionId);
    Console.WriteLine($"Listed {versions.Count} Thing Description Version(s): {string.Join(", ", versions.Select(v => v.VersionId))}");
}

// Thing Model extension: create -> get -> create -> list.
static async Task ThingModelDemo(IEdgeRegistryClient client)
{
    const string thingModelId = "sample-thing-model";
    Console.WriteLine("--- Thing Model extension ---");

    ThingModelVersion first = await client.CreateThingModelVersionAsync(
        GroupId.CloudDefault,
        thingModelId,
        new List<Label> { MakeLabel("managed-by", "sample") },
        new ThingModelVersionAttributes
        {
            Format = ThingModelFormat.JsonLd11,
            Document = Encoding.UTF8.GetBytes(ThingModelV1),
            Labels = new List<Label> { MakeLabel("revision", "1") },
            Extensions = new(),
        });
    Console.WriteLine($"Created Thing Model Version {first.VersionId} (xid {first.XId})");

    ThingModelVersion got = await client.GetThingModelVersionAsync(
        GroupId.CloudDefault,
        thingModelId,
        GetThingModelVersionId.Specific(first.VersionId));
    Console.WriteLine($"Got Thing Model Version {got.VersionId} (isDefault: {got.IsDefault})");

    ThingModelVersion second = await client.CreateThingModelVersionAsync(
        GroupId.CloudDefault,
        thingModelId,
        new List<Label> { MakeLabel("managed-by", "sample") },
        new ThingModelVersionAttributes
        {
            Format = ThingModelFormat.JsonLd11,
            Document = Encoding.UTF8.GetBytes(ThingModelV2),
            Labels = new List<Label> { MakeLabel("revision", "2") },
            Extensions = new(),
        });
    Console.WriteLine($"Created Thing Model Version {second.VersionId}");

    IReadOnlyList<ThingModelVersionXid> versions = await client.ListThingModelVersionsAsync(GroupSelector.Default, thingModelId);
    Console.WriteLine($"Listed {versions.Count} Thing Model Version(s): {string.Join(", ", versions.Select(v => v.VersionId))}");
}
