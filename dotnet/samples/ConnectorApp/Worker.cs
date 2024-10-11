// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using Azure.Iot.Operations.Services.SchemaRegistry;
using HttpConnectorWorkerService;
using System.Reflection;
using System.Text.Json;

namespace DotnetHttpConnectorWorkerService
{
    public class MachineStatusTelemetrySender : TelemetrySender<MachineStatus>
    {
        public MachineStatusTelemetrySender(IMqttPubSubClient mqttClient)
            : base(mqttClient, null, new Utf8JsonSerializer())
        {
        }
    }

    public class MachineLastMaintenanceTelemetrySender : TelemetrySender<MachineLastMaintenance>
    {
        public MachineLastMaintenanceTelemetrySender(IMqttPubSubClient mqttClient)
            : base(mqttClient, null, new Utf8JsonSerializer())
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
                AzureDeviceRegistryClient adrClient = new();
                Console.WriteLine("Successfully created ADR client");

                string assetId = "todo - doesn't matter yet";
                AssetEndpointProfile httpServerAssetEndpointProfile = await adrClient.GetAssetEndpointProfileAsync(assetId);

                // TODO the asset is not currently deployed by the operator. Stubbing out this code in the meantime
                //Asset httpServerAsset = await adrClient.GetAssetAsync(assetId);
                Asset httpServerAsset = GetStubAsset(assetId);

                Console.WriteLine("Successfully retrieved asset endpoint profile");

                string httpServerUsername = httpServerAssetEndpointProfile.Credentials!.Username!;
                byte[] httpServerPassword = httpServerAssetEndpointProfile.Credentials!.Password!;

                Dataset httpServerStatusDataset = httpServerAsset.Datasets![0];
                Dataset httpServerLastMaintenanceDataset = httpServerAsset.Datasets![1];
                TimeSpan defaultSamplingInterval = TimeSpan.FromMilliseconds(httpServerAsset.DefaultDatasetsConfiguration!.RootElement.GetProperty("samplingInterval").GetInt16());

                DataPoint httpServerMachineIdDataPoint = httpServerDataset.DataPoints![0];
                HttpMethod httpServerMachineIdHttpMethod = HttpMethod.Parse(httpServerMachineIdDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
                string httpServerMachineIdRequestPath = httpServerMachineIdDataPoint.DataSource!;
                using HttpDataRetriever httpServerMachineIdDataRetriever = new(httpServerAssetEndpointProfile.TargetAddress, httpServerMachineIdRequestPath, httpServerMachineIdHttpMethod, httpServerUsername, httpServerPassword);

                DataPoint httpServerStatusDataPoint = httpServerDataset.DataPoints![0];
                HttpMethod httpServerStatusHttpMethod = HttpMethod.Parse(httpServerMachineIdDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
                string httpServerStatusRequestPath = httpServerMachineIdDataPoint.DataSource!;
                using HttpDataRetriever httpServerStatusDataRetriever = new(httpServerAssetEndpointProfile.TargetAddress, httpServerStatusRequestPath, httpServerStatusHttpMethod, httpServerUsername, httpServerPassword);

                DataPoint httpServerMachineIdDataPoint = httpServerDataset.DataPoints![1];
                HttpMethod httpServerMachineIdHttpMethod = HttpMethod.Parse(httpServerMachineIdDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
                string httpServerMachineIdRequestPath = httpServerMachineIdDataPoint.DataSource!;
                using HttpDataRetriever httpServerMachineIdDataRetriever = new(httpServerAssetEndpointProfile.TargetAddress, httpServerMachineIdRequestPath, httpServerMachineIdHttpMethod, httpServerUsername, httpServerPassword);

                DataPoint httpServerLastMaintenanceDataPoint = httpServerDataset.DataPoints![1];
                HttpMethod httpServerStatusHttpMethod = HttpMethod.Parse(httpServerMachineIdDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
                string httpServerStatusRequestPath = httpServerMachineIdDataPoint.DataSource!;
                using HttpDataRetriever httpServerStatusDataRetriever = new(httpServerAssetEndpointProfile.TargetAddress, httpServerStatusRequestPath, httpServerStatusHttpMethod, httpServerUsername, httpServerPassword);

                // Create MQTT client from credentials provided by the operator
                MqttConnectionSettings mqttConnectionSettings = MqttConnectionSettings.FromFileMount();
                MqttSessionClient sessionClient = new();
                await sessionClient.ConnectAsync(mqttConnectionSettings);

                await using var sender = new MachineStatusTelemetrySender(sessionClient)
                {
                    TopicPattern = httpServerAsset.Datasets[0].Topic!.Path!,
                    //TODO retain?
                };

                while (true)
                {


                    await Task.Delay(samplingInterval);
                }
            }
        }

        private async Task GetMachineStatus(MachineStatusTelemetrySender telemetrySender, HttpDataRetriever httpServerMachineIdDataRetriever, HttpDataRetriever httpServerStatusDataRetriever)
        {
            // Read data from the 3rd party asset
            string httpDataMachineId = await httpServerMachineIdDataRetriever.RetrieveDataAsync();
            string httpDataStatus = await httpServerStatusDataRetriever.RetrieveDataAsync();

            string machineId = JsonDocument.Parse(httpDataMachineId).RootElement.GetProperty(httpServerMachineIdDataPoint.Name!).GetString()!;
            string status = JsonDocument.Parse(httpDataMachineId).RootElement.GetProperty(httpServerStatusDataPoint.Name!).GetString()!;

            MachineStatus machineStatus = new(machineId, status);

            await telemetrySender.SendTelemetryAsync(machineStatus);
        }

        private async Task GetMachineLastMaintenance(MachineLastMaintenanceTelemetrySender telemetrySender, HttpDataRetriever httpServerMachineIdDataRetriever)
        {
            // Read data from the 3rd party asset
            string httpDataMachineId = await httpServerMachineIdDataRetriever.RetrieveDataAsync();
            string httpDataStatus = await httpServerStatusDataRetriever.RetrieveDataAsync();

            string machineId = JsonDocument.Parse(httpDataMachineId).RootElement.GetProperty(httpServerMachineIdDataPoint.Name!).GetString()!;
            string lastMaintenance = JsonDocument.Parse(httpDataMachineId).RootElement.GetProperty(httpServerStatusDataPoint.Name!).GetString()!;

            MachineLastMaintenance machineLastMaintenance = new(machineId, lastMaintenance);

            await telemetrySender.SendTelemetryAsync(machineLastMaintenance);
        }

        private Asset GetStubAsset(string assetId)
        {
            return new()
            {
                DefaultDatasetsConfiguration = JsonDocument.Parse("{\"samplingInterval\": 10000}"),
                Datasets = new Dataset[]
                {
                    new Dataset()
                    {
                        Name = "machine_status",
                        DataPoints = new DataPoint[]
                        {
                            new DataPoint()
                            {
                                Name = "machine_id",
                                DataSource = "/api/machine/id",
                                DataPointConfiguration = JsonDocument.Parse("{\"HttpRequestMethod\":\"GET\"}"),
                            },
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
                    },
                    new Dataset()
                    {
                        Name = "last_maintenance",
                        DataPoints = new DataPoint[]
                        {
                            new DataPoint()
                            {
                                Name = "machine_id",
                                DataSource = "/api/machine/id",
                                DataPointConfiguration = JsonDocument.Parse("{\"HttpRequestMethod\":\"GET\"}"),
                            },
                            new DataPoint()
                            {
                                Name = "last_maintenance",
                                DataSource = "/api/machine/maintenance",
                                DataPointConfiguration = JsonDocument.Parse("{\"HttpRequestMethod\":\"GET\"}"),
                            },
                        },
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 10000}"),
                        Topic = new()
                        {
                            Path = "mqtt/machine/last_maintenance",
                            Retain = "Keep",
                        }
                    }
                }
            };
        }
    }
}