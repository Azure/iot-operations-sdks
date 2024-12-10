using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1;
using Azure.Iot.Operations.Services.SchemaRegistry;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Retry;

public class SchemaE2E
{
    //private const string AIONamespace = "azure-iot-operations";
    //private const string ConfigmapName = "adr-schema-registry-config";
    //private const string AioApiVersion = "2024-11-01";
    //private const string SchemaRegistryApiVersion = "2024-09-01-preview";

    private const string JsonSchema1 = """
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

    private const string JsonSchema2 = """
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

    public static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Schema E2E Execution...");

        try
        {
            // Create and connect MQTT client
            await using MqttSessionClient mqttClient = await CreateAndConnectClientAsyncFromEnvAsync("");
            var clientId = "SchemaRegistryGetAndPutCommandTestClient" + Guid.NewGuid();
            Console.WriteLine($"Client ID: {clientId}");

            if (!mqttClient.IsConnected)
            {
                Console.WriteLine("Failed to connect to MQTT broker.");
                return;
            }

            Console.WriteLine("Connected to MQTT broker.");

            // Create SchemaRegistryClient
            await using SchemaRegistryClient schemaClient = new(mqttClient);
            Dictionary<string, string> testTags = new() { { "key1", "value1" } };

            // PUT Schema
            Console.WriteLine("Uploading schema...");
            var putSchemaResponse = await schemaClient.PutAsync(
                JsonSchema1,
                Enum_Ms_Adr_SchemaRegistry_Format__1.JsonSchemaDraft07,
                Enum_Ms_Adr_SchemaRegistry_SchemaType__1.MessageSchema,
                "1",
                testTags,
                TimeSpan.FromSeconds(300),
                CancellationToken.None
            );

            Console.WriteLine($"Schema uploaded. ID: {putSchemaResponse.Name}");

            // Validate PUT Response
            if (putSchemaResponse.SchemaContent.Contains("temperature"))
            {
                Console.WriteLine("PUT Schema validation passed.");
            }

            // GET Schema
            Console.WriteLine("Retrieving schema...");
            var getSchemaResponse = await schemaClient.GetAsync(
                putSchemaResponse.Name!,
                "1",
                TimeSpan.FromSeconds(300),
                CancellationToken.None
            );

            Console.WriteLine($"Retrieved schema: {getSchemaResponse.SchemaContent}");
            if (getSchemaResponse.SchemaContent.Contains("temperature"))
            {
                Console.WriteLine("GET Schema validation passed.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("Schema E2E Execution completed.");
        }
    }

    public static async Task<MqttSessionClient> CreateAndConnectClientAsyncFromEnvAsync(string clientId)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            clientId = Guid.NewGuid().ToString();
        }

        // Debug.Assert(Environment.GetEnvironmentVariable("MQTT_TEST_BROKER_CS") != null);
        string cs = $"{Environment.GetEnvironmentVariable("MQTT_TEST_BROKER_CS")};ClientId=schemaclient";
        //string cs = "HostName=48.214.65.114;TcpPort=1883;UseTls=false;ClientId=schemaclient";
        MqttConnectionSettings mcs = MqttConnectionSettings.FromConnectionString(cs);
        MqttSessionClientOptions sessionClientOptions = new MqttSessionClientOptions()
        {
            // This retry policy prevents the client from retrying forever
            ConnectionRetryPolicy = new ExponentialBackoffRetryPolicy(10, TimeSpan.FromSeconds(5)),
            RetryOnFirstConnect = true, // This helps counteract if MQ is still deploying when the test is run
            EnableMqttLogging = true,
        };

        MqttSessionClient mqttSessionClient = new(sessionClientOptions);
        await mqttSessionClient.ConnectAsync(mcs);
        return mqttSessionClient;
    }
}
