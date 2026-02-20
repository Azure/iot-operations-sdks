// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.IntegrationTest;

using Azure.Iot.Operations.Services.SchemaRegistry;
using Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Mqtt.Session;
using Xunit;
using Xunit.Abstractions;
using SchemaFormat = Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry.Format;
using SchemaType = Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry.SchemaType;
using Azure.Iot.Operations.Services.SchemaRegistry.Models;
using SchemaRegistryErrorException = SchemaRegistry.Models.SchemaRegistryErrorException;

[Trait("Category", "SchemaRegistry")]
public class SchemaRegistryClientIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task JsonRegisterGet()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using ISchemaRegistryClient client = new SchemaRegistryClient(applicationContext, mqttClient);
        Dictionary<string, string> testTags = new() { { "key1", "value1" } };

        Schema? res = await client.PutAsync(jsonSchema1, SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema, "1", testTags);
        output.WriteLine($"resp {res?.Name}");
        //Assert.Equal("29F37966A94F76DB402A96BC5D9B2B3A5B9465CA2A80696D7DE40AEB3DE8E9E7", res.Name);
        string schemaId = res?.Name!;
        Schema? getSchemaResponse = await client.GetAsync(schemaId, "1");

        output.WriteLine($"getRes {res?.Version}");
        Assert.Contains("temperature", getSchemaResponse?.SchemaContent);
        Assert.Equal(SchemaFormat.JsonSchemaDraft07, getSchemaResponse?.Format);
        Assert.Equal(SchemaType.MessageSchema, getSchemaResponse?.SchemaType);
        Assert.Equal(jsonSchema1, getSchemaResponse?.SchemaContent);
        Assert.NotNull(getSchemaResponse?.Tags);
        Assert.Equal("value1", getSchemaResponse.Tags.GetValueOrDefault("key1"));
        Assert.Equal("DefaultSRNamespace", getSchemaResponse.Namespace);
    }

    [Fact]
    public async Task NotFoundSchemaReturnsNull()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using SchemaRegistryClient client = new(applicationContext, mqttClient);

        await Assert.ThrowsAsync<SchemaRegistryErrorException>(async () => await client.GetAsync("NotFound"));
    }

    [Fact]
    public async Task RegisterAvroAsJsonThrows()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using ISchemaRegistryClient client = new SchemaRegistryClient(applicationContext, mqttClient);

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(async () => await client.PutAsync(avroSchema1, SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema, "1", null!, null, null, TimeSpan.FromMinutes(1)));
        Assert.True(ex.IsRemote);
        Assert.StartsWith("Invalid JsonSchema/draft-07 schema", ex.Message);
    }

    [Fact]
    public async Task InvalidJsonThrows()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using ISchemaRegistryClient client = new SchemaRegistryClient(applicationContext, mqttClient);

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(async () => await client.PutAsync("not-json}", SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema, "1", null!, null, null, TimeSpan.FromMinutes(1)));
        Assert.True(ex.IsRemote);
        Assert.StartsWith("Invalid JsonSchema/draft-07 schema", ex.Message);
    }

    [Fact]
    public async Task SchemaRegistryClientThrowsIfAccessedWhenDisposed()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using ISchemaRegistryClient client = new SchemaRegistryClient(applicationContext, mqttClient);

        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await client.PutAsync("irrelevant", SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema, "1", null!, null, null, TimeSpan.FromMinutes(1)));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await client.GetAsync("irrelevant"));
    }

    [Fact]
    public async Task SchemaRegistryClientThrowsIfCancellationRequested()
    {
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using ISchemaRegistryClient client = new SchemaRegistryClient(applicationContext, mqttClient);

        CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await client.PutAsync("irrelevant", SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema, "1", null!, null, null, TimeSpan.FromMinutes(1), cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await client.GetAsync("irrelevant", cancellationToken: cts.Token));
    }

    [Fact]
    public async Task TestDefaults()
    {
        string defaultSchemaVersion = "1";

        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();

        // Test using the default params as defined by the interface of the schema registry client
        await using ISchemaRegistryClient schemaRegistryInterface = new SchemaRegistryClient(applicationContext, mqttClient);

        Schema? res = await schemaRegistryInterface.PutAsync(jsonSchema1, SchemaFormat.JsonSchemaDraft07);
        string schemaId = res?.Name!;
        Schema? getSchemaResponse = await schemaRegistryInterface.GetAsync(schemaId);

        output.WriteLine($"getRes {res?.Version}");
        Assert.Contains("temperature", getSchemaResponse?.SchemaContent);
        Assert.Equal(SchemaFormat.JsonSchemaDraft07, getSchemaResponse?.Format);
        Assert.Equal(SchemaType.MessageSchema, getSchemaResponse?.SchemaType);
        Assert.Equal(jsonSchema1, getSchemaResponse?.SchemaContent);
        Assert.NotNull(getSchemaResponse);
        Assert.Equal("DefaultSRNamespace", getSchemaResponse.Namespace);
        Assert.Equal(defaultSchemaVersion, getSchemaResponse.Version);

        // Test using the default params as defined by the concrete implementation of the schema registry client
        await using SchemaRegistryClient schemaRegistryImplementation = new SchemaRegistryClient(applicationContext, mqttClient);

        Schema? res2 = await schemaRegistryInterface.PutAsync(jsonSchema2, SchemaFormat.JsonSchemaDraft07);
        string schemaId2 = res2?.Name!;
        Schema? getSchemaResponse2 = await schemaRegistryInterface.GetAsync(schemaId2);

        output.WriteLine($"getRes {res2?.Version}");
        Assert.Contains("temperature", getSchemaResponse2?.SchemaContent);
        Assert.Equal(SchemaFormat.JsonSchemaDraft07, getSchemaResponse2?.Format);
        Assert.Equal(SchemaType.MessageSchema, getSchemaResponse2?.SchemaType);
        Assert.Equal(jsonSchema2, getSchemaResponse2?.SchemaContent);
        Assert.NotNull(getSchemaResponse2);
        Assert.Equal("DefaultSRNamespace", getSchemaResponse2.Namespace);
        Assert.Equal(defaultSchemaVersion, getSchemaResponse2.Version);
    }

    private static readonly string jsonSchema1 = """
    {
        "$schema": "https://json-schema.org/draft-07/schema#",
        "type": "object",
        "properties": {
      	  "humidity": {
        	    "type": "string"
        	},
        	"temperature": {
            	"type": "number"
        	}
        }
    }
    """;

    private static readonly string jsonSchema2 = """
    {
        "$schema": "https://json-schema.org/draft-07/schema#",
        "type": "object",
        "properties": {
      	  "humidity2": {
        	    "type": "string"
        	},
        	"temperature2": {
            	"type": "number"
        	}
        }
    }
    """;

    private static readonly string avroSchema1 = """
    {
        "type": "record",
        "name": "Weather",
        "fields": [
            {"name": "humidity", "type": "string"},
            {"name": "temperature", "type": "int"}
        ]
    }
    """;
}
