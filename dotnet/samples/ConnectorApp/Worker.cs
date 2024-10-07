// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using Azure.Iot.Operations.Services.SchemaRegistry;
using System.Text.Json;

namespace DotnetHttpConnectorWorkerService
{
    public class StringTelemetrySender : TelemetrySender<string>
    {
        public StringTelemetrySender(IMqttPubSubClient mqttClient)
            : base(mqttClient, "test", new Utf8JsonSerializer())
        {
        }
    }

    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // ADR client stub
                AzureDeviceRegistryClient adrClient = new();
                Console.WriteLine("Successfully created ADR client");

                string assetId = "todo - doesn't matter yet";
                AssetEndpointProfile aep = await adrClient.GetAssetEndpointProfileAsync(assetId);
                Console.WriteLine("Successfully retrieved asset endpoint profile");

                JsonDocument? additionalConfiguration = null;

                HttpDataRetriever httpDataRetriever = new(aep.TargetAddress, "todo", aep.Credentials?.Username ?? "", aep.Credentials?.Password ?? Array.Empty<byte>());

                // Create MQTT client from credentials provided by the operator
                MqttConnectionSettings mqttConnectionSettings = MqttConnectionSettings.FromFileMount();
                MqttSessionClient sessionClient = new();
                await sessionClient.ConnectAsync(mqttConnectionSettings);

                while (true)
                {
                    // Read data from the 3rd party asset
                    string httpData = await httpDataRetriever.RetrieveDataAsync();

                    var sender = new StringTelemetrySender(sessionClient)
                    {
                        TopicPattern = "sample",
                        ModelId = "someModel",
                    };

                    for (int i = 0; i < 5; i++)
                    {
                        await sender.SendTelemetryAsync(httpData);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
        }
    }
}