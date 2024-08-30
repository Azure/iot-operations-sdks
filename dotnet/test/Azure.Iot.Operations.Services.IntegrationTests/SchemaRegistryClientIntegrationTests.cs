namespace Azure.Iot.Operations.Services.IntegrationTest;

using Azure.Iot.Operations.Services.SchemaRegistry;
using Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Mqtt.Session;
using Xunit;
using Xunit.Abstractions;
using SchemaFormat = Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1.Enum_Ms_Adr_SchemaRegistry_Format__1;
using SchemaType = Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1.Enum_Ms_Adr_SchemaRegistry_SchemaType__1;

[Trait("Category", "SchemaRegistry")]
public class SchemaRegistryClientIntegrationTests(ITestOutputHelper output)
{
    const string Version1_0_0 = "1.0.0";

    [Fact]
    public async Task JsonRegisterGet()
    {
        await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        await using SchemaRegistryClient client = new(_mqttClient);
        Dictionary<string, string> testTags = new() { { "key1", "value1" } };

        Object_Ms_Adr_SchemaRegistry_Schema__1 res = await client.PutAsync(jsonSchema1, SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema, Version1_0_0, testTags);
        output.WriteLine($"resp {res.Name}");
        //Assert.Equal("29F37966A94F76DB402A96BC5D9B2B3A5B9465CA2A80696D7DE40AEB3DE8E9E7", res.Name);
        string schemaId = res.Name!;
        Object_Ms_Adr_SchemaRegistry_Schema__1 getSchemaResponse = await client.GetAsync(schemaId, Version1_0_0);

        output.WriteLine($"getRes {res.Version}");
        Assert.Contains("temperature", getSchemaResponse.SchemaContent);
        Assert.Equal(SchemaFormat.JsonSchemaDraft07, getSchemaResponse.Format);
        Assert.Equal(SchemaType.MessageSchema, getSchemaResponse.SchemaType);
        Assert.Equal(jsonSchema1, getSchemaResponse.SchemaContent);
        Assert.NotNull(getSchemaResponse.Tags);
        Assert.Equal("value1", getSchemaResponse.Tags.GetValueOrDefault("key1"));
        Assert.Equal("DefaultSRNamespace", getSchemaResponse.Namespace);
    }

    [Fact]
    public async Task NotFoundSchemaReturnsNull()
    {
        await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        await using SchemaRegistryClient client = new(_mqttClient);
        
        Object_Ms_Adr_SchemaRegistry_Schema__1 s = await client.GetAsync("NotFound");
        Assert.Null(s);
    }

    [Fact]
    public async Task RegisterAvroAsJsonThrows()
    {
        await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        await using SchemaRegistryClient client = new(_mqttClient);

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(async () => await client.PutAsync(avroSchema1, SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema, Version1_0_0, null!, TimeSpan.FromMinutes(1)));
        Assert.True(ex.IsRemote);
        Assert.StartsWith("Invalid JsonSchemaDraft07 schema", ex.Message);
    }

    [Fact]
    public async Task InvalidJsonThrows()
    {
        await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        await using SchemaRegistryClient client = new(_mqttClient);

        AkriMqttException ex = await Assert.ThrowsAsync<AkriMqttException>(async () => await client.PutAsync("not-json}", SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema, Version1_0_0, null!, TimeSpan.FromMinutes(1)));
        Assert.True(ex.IsRemote);
        Assert.StartsWith("Invalid JsonSchemaDraft07 schema", ex.Message);
    }

    [Fact]
    public async Task SchemaRegistryClientThrowsIfAccessedWhenDisposed()
    {
        await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        await using SchemaRegistryClient client = new(_mqttClient);

        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await client.PutAsync("irrelevant", SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema, Version1_0_0, null!, TimeSpan.FromMinutes(1)));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await client.GetAsync("irrelevant"));
    }

    [Fact]
    public async Task SchemaRegistryClientThrowsIfCancellationRequested()
    {
        await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
        await using SchemaRegistryClient client = new(_mqttClient);

        CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await client.PutAsync("irrelevant", SchemaFormat.JsonSchemaDraft07, SchemaType.MessageSchema, Version1_0_0, null!, TimeSpan.FromMinutes(1), cts.Token));
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

