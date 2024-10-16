// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using Azure.Iot.Operations.Services.SchemaRegistry;
using System.Text.Json;

namespace Azure.Iot.Operations.ConnectorSample
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

    public class HttpConnectorWorkerService : BackgroundService
    {
        private readonly ILogger<HttpConnectorWorkerService> _logger;
        private MqttSessionClient _sessionClient;

        private Asset? _httpServerAsset;
        private AssetEndpointProfile? _httpServerAssetEndpointProfile;

        public HttpConnectorWorkerService(ILogger<HttpConnectorWorkerService> logger, MqttSessionClient mqttSessionClient)
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

                // TODO unimplemented so far
                //await adrClient.ObserveAssetAsync(assetId);
                await adrClient.ObserveAssetEndpointProfileAsync(assetId);

                _logger.LogInformation("Successfully retrieved asset endpoint profile");

                TimeSpan defaultSamplingInterval = TimeSpan.FromMilliseconds(_httpServerAsset.DefaultDatasetsConfiguration!.RootElement.GetProperty("samplingInterval").GetInt16());

                // Create MQTT client from credentials provided by the operator
                MqttConnectionSettings mqttConnectionSettings = MqttConnectionSettings.FromFileMount();

                _logger.LogInformation($"Connecting to MQTT broker with {mqttConnectionSettings}");

                await _sessionClient.ConnectAsync(mqttConnectionSettings);

                _logger.LogInformation($"Successfully connected to MQTT broker");

                string datasetName = "thermostat_status";
                Dataset thermostatDataset = _httpServerAsset.Datasets![datasetName];
                TimeSpan samplingInterval = defaultSamplingInterval;
                if (thermostatDataset.DatasetConfiguration != null
                    && thermostatDataset.DatasetConfiguration.RootElement.TryGetProperty("samplingInterval", out JsonElement datasetSpecificSamplingInterval))
                {
                    samplingInterval = TimeSpan.FromMilliseconds(datasetSpecificSamplingInterval.GetInt16());
                }

                _logger.LogInformation($"Will sample dataset with name {0} at a rate of once per {1} milliseconds", datasetName, (int)samplingInterval.TotalMilliseconds);
                using Timer datasetSamplingTimer = new(SampleThermostatStatus, datasetName, 0, (int)samplingInterval.TotalMilliseconds);

                // Wait until the worker is cancelled
                await Task.Delay(TimeSpan.MaxValue, cancellationToken);
            }
            finally
            {
                _logger.LogInformation("Shutting down sample...");

                // TODO unimplemented so far
                //await adrClient.UnobserveAssetAsync(assetId);
                await adrClient.UnobserveAssetEndpointProfileAsync(assetId);
            }
        }

        private async void SampleThermostatStatus(object? status)
        {
            Dataset httpServerStatusDataset = _httpServerAsset!.Datasets!["thermostat_status"];

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

        private Asset GetStubAsset()
        {
            return new()
            {
                DefaultDatasetsConfiguration = JsonDocument.Parse("{\"samplingInterval\": 400}"),
                Datasets = new Dictionary<string, Dataset>
                {
                    {
                        "thermostat_status",
                        new Dataset()
                        {
                            DataPoints = new DataPoint[]
                            {
                                new DataPoint("/api/machine/my_thermostat_1/status", "actual_temperature")
                                {
                                    DataPointConfiguration = JsonDocument.Parse("{\"HttpRequestMethod\":\"GET\"}"),
                                },
                                new DataPoint("/api/machine/my_thermostat_1/status", "desired_temperature")
                                {
                                    DataPointConfiguration = JsonDocument.Parse("{\"HttpRequestMethod\":\"GET\"}"),
                                },
                            },
                            Topic = new()
                            {
                                Path = "mqtt/machine/my_thermostat_1/status",
                                Retain = "Keep",
                            }
                        }
                    }
                }
            };
        }
    }
}