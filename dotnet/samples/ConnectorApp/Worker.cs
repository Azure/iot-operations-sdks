// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using Azure.Iot.Operations.Services.SchemaRegistry;
using System.Text.Json;

namespace HttpConnectorWorkerService
{
    //TODO sample only works if you assume some aspects of the asset + AEP don't change such as sampling interval, http paths, datasets in general, etc. Probably want to document this somehow
    // Sample also assumes the order of asset datasets + datapoints which feels bad. Name key on each isn't specifically unique, so can't key off of that, right? Ask ADR folks. I may be able
    // to make it so that it is a map instead of an array so that each sampling function only needs the name to key off of and doesn't assume ordering
    public class ThermostatStatusTelemetrySender : TelemetrySender<ThermostatStatus>
    {
        public ThermostatStatusTelemetrySender(IMqttPubSubClient mqttClient)
            : base(mqttClient, null, new Utf8JsonSerializer())
        {
        }
    }

    public class ThermostatLastMaintenanceTelemetrySender : TelemetrySender<MachineLastMaintenance>
    {
        public ThermostatLastMaintenanceTelemetrySender(IMqttPubSubClient mqttClient)
            : base(mqttClient, null, new Utf8JsonSerializer())
        {
        }
    }

    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private MqttSessionClient _sessionClient;

        private Asset? _httpServerAsset;
        private AssetEndpointProfile? _httpServerAssetEndpointProfile;

        public Worker(ILogger<Worker> logger, MqttSessionClient mqttSessionClient)
        {
            _logger = logger;
            _sessionClient = mqttSessionClient;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            AzureDeviceRegistryClient adrClient = new();
            _logger.LogInformation("Successfully created ADR client");
            
            string assetId = "todo - doesn't matter yet";

            try
            {
                _httpServerAssetEndpointProfile = await adrClient.GetAssetEndpointProfileAsync(assetId);

                // TODO the asset is not currently deployed by the operator. Stubbing out this code in the meantime
                //_httpServerAsset = await _adrClient.GetAssetAsync(assetId);
                _httpServerAsset = GetStubAsset();

                adrClient.AssetChanged += (sender, newAsset) =>
                {
                    _logger.LogInformation("Recieved a notification that the asset has changed.");
                    _httpServerAsset = newAsset;
                };

                adrClient.AssetEndpointProfileChanged += (sender, newAssetEndpointProfile) =>
                {
                    _logger.LogInformation("Recieved a notification that the asset endpoint definition has changed.");
                    _httpServerAssetEndpointProfile = newAssetEndpointProfile;
                };

                await adrClient.ObserveAssetAsync(assetId);
                await adrClient.ObserveAssetEndpointProfileAsync(assetId);

                _logger.LogInformation("Successfully retrieved asset endpoint profile");

                TimeSpan defaultSamplingInterval = TimeSpan.FromMilliseconds(_httpServerAsset.DefaultDatasetsConfiguration!.RootElement.GetProperty("samplingInterval").GetInt16());

                // Create MQTT client from credentials provided by the operator
                MqttConnectionSettings mqttConnectionSettings = MqttConnectionSettings.FromFileMount();

                _logger.LogInformation($"Connecting to MQTT broker with {mqttConnectionSettings}");

                await _sessionClient.ConnectAsync(mqttConnectionSettings);

                _logger.LogInformation($"Successfully connected to MQTT broker");

                Timer[] datasetSamplingTimers = new Timer[_httpServerAsset.Datasets!.Length];
                for (int datasetIndex = 0; datasetIndex < _httpServerAsset.Datasets.Length; datasetIndex++)
                {
                    Dataset thermostatDataset = _httpServerAsset.Datasets[datasetIndex];
                    TimeSpan samplingInterval = defaultSamplingInterval;
                    if (thermostatDataset.DatasetConfiguration != null
                        && thermostatDataset.DatasetConfiguration.RootElement.TryGetProperty("samplingInterval", out JsonElement datasetSpecificSamplingInterval))
                    {
                        samplingInterval = TimeSpan.FromMilliseconds(datasetSpecificSamplingInterval.GetInt16());
                    }

                    Timer datasetSamplingTimer = new(SampleAsync, datasetIndex, 0, (int)samplingInterval.TotalMilliseconds);
                    datasetSamplingTimers[datasetIndex] = datasetSamplingTimer;
                }

                // Wait until the worker is cancelled
                await Task.Delay(TimeSpan.MaxValue, cancellationToken);
            }
            finally
            {
                _logger.LogInformation("Shutting down sample...");

                await adrClient.UnobserveAssetAsync(assetId);
                await adrClient.UnobserveAssetEndpointProfileAsync(assetId);
            }
        }

        private async void SampleAsync(object? state)
        {
            //TODO mapping via name instead would be a lot better
            int datasetIndex = (int)state!;
            if (datasetIndex == 1)
            {
                await ForwardThermostatStatus();
            }
            else
            {
                await ForwardThermostatLastMaintenance();
            }
        }

        private async Task ForwardThermostatStatus()
        {
            // TODO the asset is not currently deployed by the operator. Stubbing out this code in the meantime
            //Asset httpServerAsset = await _adrClient.GetAssetAsync(assetId);
            Asset httpServerAsset = GetStubAsset();
            Dataset httpServerStatusDataset = httpServerAsset.Datasets![0];

            await using var thermostateStatusSender = new ThermostatStatusTelemetrySender(_sessionClient)
            {
                TopicPattern = httpServerStatusDataset.Topic!.Path!,
                //TODO retain? Asset docs say it is a topic level attribute, but doesn't match MQTT spec which says it is message-level
            };

            string httpServerUsername = _httpServerAssetEndpointProfile!.Credentials!.Username!;
            byte[] httpServerPassword = _httpServerAssetEndpointProfile.Credentials!.Password!;

            DataPoint httpServerDesiredTemperatureDataPoint = httpServerStatusDataset.DataPoints![0];
            HttpMethod httpServerDesiredTemperatureHttpMethod = HttpMethod.Parse(httpServerDesiredTemperatureDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
            string httpServerDesiredTemperatureRequestPath = httpServerDesiredTemperatureDataPoint.DataSource!;
            using HttpDataRetriever httpServerDesiredTemperatureDataRetriever = new(_httpServerAssetEndpointProfile.TargetAddress, httpServerDesiredTemperatureRequestPath, httpServerDesiredTemperatureHttpMethod, httpServerUsername, httpServerPassword);

            DataPoint httpServerActualTemperatureDataPoint = httpServerStatusDataset.DataPoints![1];
            HttpMethod httpServerActualTemperatureHttpMethod = HttpMethod.Parse(httpServerActualTemperatureDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
            string httpServerActualTemperatureRequestPath = httpServerActualTemperatureDataPoint.DataSource!;
            using HttpDataRetriever httpServerActualTemperatureDataRetriever = new(_httpServerAssetEndpointProfile.TargetAddress, httpServerActualTemperatureRequestPath, httpServerActualTemperatureHttpMethod, httpServerUsername, httpServerPassword);

            _logger.LogInformation($"Reading thermostat status from HTTP server asset");
            string desiredTemperatureValue = await httpServerDesiredTemperatureDataRetriever.RetrieveDataAsync(httpServerDesiredTemperatureDataPoint.Name);
            string actualTemperatureValue = await httpServerActualTemperatureDataRetriever.RetrieveDataAsync(httpServerActualTemperatureDataPoint.Name);

            var thermostatStatus = new ThermostatStatus(desiredTemperatureValue, actualTemperatureValue);
            _logger.LogInformation($"Successfully read thermostat status from HTTP server asset: {thermostatStatus}");

            _logger.LogInformation($"Sending thermostat status to MQTT broker");
            await thermostateStatusSender.SendTelemetryAsync(thermostatStatus);
        }

        private async Task ForwardThermostatLastMaintenance()
        {
            Dataset httpServerLastMaintenanceDataset = _httpServerAsset!.Datasets![1];
            await using var lastMaintenanceSender = new ThermostatLastMaintenanceTelemetrySender(_sessionClient)
            {
                TopicPattern = httpServerLastMaintenanceDataset.Topic!.Path!,
                //TODO retain? Asset docs say it is a topic level attribute, but doesn't match MQTT spec which says it is message-level
            };

            string httpServerUsername = _httpServerAssetEndpointProfile!.Credentials!.Username!;
            byte[] httpServerPassword = _httpServerAssetEndpointProfile.Credentials!.Password!;

            DataPoint httpServerMaintenanceDataPoint = httpServerLastMaintenanceDataset.DataPoints![0];
            HttpMethod httpServerMaintenanceHttpMethod = HttpMethod.Parse(httpServerMaintenanceDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
            string httpServerMaintenanceRequestPath = httpServerMaintenanceDataPoint.DataSource!;
            using HttpDataRetriever httpServerMaintenanceDataRetriever = new(_httpServerAssetEndpointProfile.TargetAddress, httpServerMaintenanceRequestPath, httpServerMaintenanceHttpMethod, httpServerUsername, httpServerPassword);

            _logger.LogInformation($"Reading thermostat's last maintenance from HTTP server asset");
            var lastMaintenance = new MachineLastMaintenance(await httpServerMaintenanceDataRetriever.RetrieveDataAsync(httpServerMaintenanceDataPoint.Name));

            _logger.LogInformation($"Successfully read thermostat last maintenance from HTTP server asset: {lastMaintenance}");

            _logger.LogInformation($"Sending thermostat last maintenance to MQTT broker");
            await lastMaintenanceSender.SendTelemetryAsync(lastMaintenance);
        }

        private Asset GetStubAsset()
        {
            return new()
            {
                DefaultDatasetsConfiguration = JsonDocument.Parse("{\"samplingInterval\": 400}"),
                Datasets = new Dataset[]
                {
                    new Dataset()
                    {
                        Name = "machine_status",
                        DataPoints = new DataPoint[]
                        {
                            new DataPoint()
                            {
                                Name = "actual_temperature",
                                DataSource = "/api/machine/my_thermostat_1/status",
                                DataPointConfiguration = JsonDocument.Parse("{\"HttpRequestMethod\":\"GET\"}"),
                            },
                            new DataPoint()
                            {
                                Name = "desired_temperature",
                                DataSource = "/api/machine/my_thermostat_1/status",
                                DataPointConfiguration = JsonDocument.Parse("{\"HttpRequestMethod\":\"GET\"}"),
                            },
                        },
                        Topic = new()
                        {
                            Path = "mqtt/machine/my_thermostat_1/status",
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
                                Name = "last_maintenance",
                                DataSource = "/api/machine/my_thermostat_1/maintenance",
                                DataPointConfiguration = JsonDocument.Parse("{\"HttpRequestMethod\":\"GET\"}"),
                            },
                        },
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 10000}"),
                        Topic = new()
                        {
                            Path = "mqtt/machine/my_thermostat_1/last_maintenance",
                            Retain = "Keep",
                        }
                    }
                }
            };
        }
    }
}