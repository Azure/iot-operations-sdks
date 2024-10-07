// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using Azure.Iot.Operations.Services.SchemaRegistry;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // ADR client stub
                AzureDeviceRegistryClient adrClient = new();
                Console.WriteLine("Successfully created ADR client");

                string assetId = "todo - doesn't matter yet";
                AssetEndpointProfile httpServerAssetEndpointProfile = await adrClient.GetAssetEndpointProfileAsync(assetId);

                // TODO the asset is not currently deployed by the operator. Stubbing out this code in the meantime
                //Asset httpServerAsset = await adrClient.GetAssetAsync(assetId);
                Asset httpServerAsset = GetStubAsset(assetId);

                Console.WriteLine("Successfully retrieved asset endpoint profile");

                if (httpServerAssetEndpointProfile.AdditionalConfiguration == null)
                {
                    throw new InvalidOperationException("Expected some additional configuration field in the asset endpoint profile");
                }

                if (httpServerAssetEndpointProfile.Credentials == null || httpServerAssetEndpointProfile.Credentials.Username == null || httpServerAssetEndpointProfile.Credentials.Password == null)
                { 
                    throw new InvalidOperationException("Expected an asset endpoint username and password.");
                }

                string httpServerUsername = httpServerAssetEndpointProfile.Credentials.Username;
                byte[] httpServerPassword = httpServerAssetEndpointProfile.Credentials.Password;

                Dataset httpServerDataset = httpServerAsset.Datasets[0];
                DataPoint httpServerDataPoint = httpServerDataset.DataPoints[0];

                HttpMethod httpMethod = HttpMethod.Parse(httpServerDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
                string httpServerRequestPath = httpServerDataPoint.DataSource;
                HttpDataRetriever httpDataRetriever = new(httpServerAssetEndpointProfile.TargetAddress, httpServerRequestPath, httpMethod, httpServerUsername, httpServerPassword);

                MqttConnectionSettings mqttConnectionSettings = null;
                MqttSessionClient sessionClient = null;

                await sessionClient.ConnectAsync(mqttConnectionSettings);

                while (true)
                {
                    // Read data from the 3rd party asset
                    string httpData = await httpDataRetriever.RetrieveDataAsync();

                    Console.WriteLine("Read data from http asset endpoint:");
                    Console.WriteLine(httpData + "\n");
                    
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

        private Asset GetStubAsset(string assetId)
        {
            return new()
            {
                Datasets = new Dataset[]
                {
                    new Dataset()
                    {
                        Name = "machine_status",
                        DataPoints = new DataPoint[]
                        {
                            new DataPoint()
                            {
                                Name = "status",
                                DataSource = "/api/machine/status",
                                DataPointConfiguration = JsonDocument.Parse("{\"HttpRequestMethod\":\"GET\"}"),
                            },
                        },
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 400}"),
                        Topic = new()
                        {
                            Path = "mqtt/machine/status",
                            Retain = "Keep",
                        }
                    }
                }
            };
        }
    }
}
