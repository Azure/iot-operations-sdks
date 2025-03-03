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

[Trait("Category", "SchemaRegistry")]
public class SchemaRegistryClientIntegrationTests(ITestOutputHelper output)
{
    const string DefaultVersion = "1";

    [Fact]
    public async Task JsonRegisterGet()
    {
        await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new();
        await using SchemaRegistryClient client = new(applicationContext, _mqttClient);
        Dictionary<string, string> testTags = new() { { "key1", "value1" } };

        Schema? res = await client.PutAsync(jsonSchema1, SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema, DefaultVersion, testTags);
        output.WriteLine($"resp {res?.Name}");
        //Assert.Equal("29F37966A94F76DB402A96BC5D9B2B3A5B9465CA2A80696D7DE40AEB3DE8E9E7", res.Name);
        string schemaId = res?.Name!;
        Schema? getSchemaResponse = await client.GetAsync(schemaId, DefaultVersion);

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
        await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new();
        await using SchemaRegistryClient client = new(applicationContext, _mqttClient);

        Schema? s = await client.GetAsync("NotFound");
        Assert.Null(s);
    }

    [Fact]
    public async Task RegisterAvroAsJsonThrows()
    {
        await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new();
        await using SchemaRegistryClient client = new(applicationContext, _mqttClient);

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(async () => await client.PutAsync(avroSchema1, SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema, DefaultVersion, null!, TimeSpan.FromMinutes(1)));
        Assert.True(ex.IsRemote);
        Assert.StartsWith("Invalid JsonSchemaDraft07 schema", ex.Message);
    }

    [Fact]
    public async Task InvalidJsonThrows()
    {
        await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new();
        await using SchemaRegistryClient client = new(applicationContext, _mqttClient);

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(async () => await client.PutAsync("not-json}", SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema, DefaultVersion, null!, TimeSpan.FromMinutes(1)));
        Assert.True(ex.IsRemote);
        Assert.StartsWith("Invalid JsonSchemaDraft07 schema", ex.Message);
    }

    [Fact]
    public async Task SchemaRegistryClientThrowsIfAccessedWhenDisposed()
    {
        await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new();
        await using SchemaRegistryClient client = new(applicationContext, _mqttClient);

        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await client.PutAsync("irrelevant", SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema, DefaultVersion, null!, TimeSpan.FromMinutes(1)));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await client.GetAsync("irrelevant"));
    }

    [Fact]
    public async Task SchemaRegistryClientThrowsIfCancellationRequested()
    {
        await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        ApplicationContext applicationContext = new();
        await using SchemaRegistryClient client = new(applicationContext, _mqttClient);

        CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await client.PutAsync("irrelevant", SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema, DefaultVersion, null!, TimeSpan.FromMinutes(1), cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await client.GetAsync("irrelevant", cancellationToken: cts.Token));
    }

    static readonly string jsonSchema1 = """
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

    static readonly string avroSchema1 = """
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
