using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1;
using Azure.Iot.Operations.Services.SchemaRegistry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Services.IntegrationTest;

namespace Azure.Iot.Operations.Services.IntegrationTests
{
    public class SchemaE2E(ITestOutputHelper output)
    {
        private readonly ITestOutputHelper _output = output;
        public const string AIONamespace = "azure-iot-operations";
        internal string ServerUri = "48.214.65.114"; // Env.MQDefaultHost();
        internal int PortTls = 8883; // int.Parse(Env.MQDefaultPortTls());
        internal int PortNonTls = 1883; // int.Parse(Env.MQPortNonTls());
        internal string ConfigmapName = "adr-schema-registry-config";
        internal string AioApiVersion = "2024-11-01";
        internal string SchemaRegistryApiVersion = "2024-09-01-preview";

        private const string _jsonSchema1 = """
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

        private const string _jsonSchema2 = """
        {
            "$schema": "https://json-schema.org/draft-07/schema#",
            "type": "object",
            "properties": {
                "humidity": {
                    "type": "string"
                }
            }
        }
        """;
        

        [Fact]
        public async Task SchemaRegistryGetAndPutCommandTest()
        {
            await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("");
            var clientId = "SchemaRegistryGetAndPutCommandTestClient" + Guid.NewGuid().ToString();
            this._output.WriteLine($"Client ID: {clientId}");

            //var connected = await client.ConnectAsync(this.ServerUri, this.PortTls, clientId, 300, true, CancellationToken.None);

            // Following commented line is for local testing
            // var connected = await client.ConnectAsync(new MqttClientOptionsBuilder().WithMqttConnectionSettings(settings).Build(), CancellationToken.None);
            Assert.True(_mqttClient.IsConnected);
            string responseReceived = string.Empty;

            await using SchemaRegistryClient schemaClient = new(_mqttClient);
            Dictionary<string, string> testTags = new() { { "key1", "value1" } };

            // TODO: Reduce request timeout to 120 seconds after resolving the timeout issue - https://msazure.visualstudio.com/One/_workitems/edit/30227439
            Object_Ms_Adr_SchemaRegistry_Schema__1 putSchemaResponse = await schemaClient.PutAsync(_jsonSchema1, Enum_Ms_Adr_SchemaRegistry_Format__1.JsonSchemaDraft07, Enum_Ms_Adr_SchemaRegistry_SchemaType__1.MessageSchema, "1", testTags, TimeSpan.FromSeconds(300), CancellationToken.None);
            Assert.Contains("temperature", putSchemaResponse.SchemaContent);
            Assert.Equal(Enum_Ms_Adr_SchemaRegistry_Format__1.JsonSchemaDraft07, putSchemaResponse.Format);
            Assert.Equal(Enum_Ms_Adr_SchemaRegistry_SchemaType__1.MessageSchema, putSchemaResponse.SchemaType);
            Assert.Equal(_jsonSchema1, putSchemaResponse.SchemaContent);
            Assert.NotNull(putSchemaResponse.Tags);
            Assert.Equal("value1", putSchemaResponse.Tags.GetValueOrDefault("key1"));

            string schemaId = putSchemaResponse.Name!;
            Object_Ms_Adr_SchemaRegistry_Schema__1 getSchemaResponse = await schemaClient.GetAsync(schemaId, "1", TimeSpan.FromSeconds(300), CancellationToken.None);

            Console.WriteLine($"getRes {putSchemaResponse.Version}");
            Assert.Contains("temperature", getSchemaResponse.SchemaContent);
            Assert.Equal(Enum_Ms_Adr_SchemaRegistry_Format__1.JsonSchemaDraft07, getSchemaResponse.Format);
            Assert.Equal(Enum_Ms_Adr_SchemaRegistry_SchemaType__1.MessageSchema, getSchemaResponse.SchemaType);
            Assert.Equal(_jsonSchema1, getSchemaResponse.SchemaContent);
            Assert.NotNull(getSchemaResponse.Tags);
            Assert.Equal("value1", getSchemaResponse.Tags.GetValueOrDefault("key1"));

            // MqttConnectionSettings settings = new MqttConnectionSettings(server)
            // {
            //     TcpPort = port,
            //     ClientId = clientId,
            //     CleanStart = cleanSession,
            //     SessionExpiry = TimeSpan.FromSeconds(sessionExpiryInterval),
            //     SatAuthFile = Env.MQSATPath(),
            //     CaFile = Env.AioCaCertPath(),
            // };
        }
    }
}
