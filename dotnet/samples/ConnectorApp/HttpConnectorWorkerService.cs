// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.ConnectorSample
{
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

            //TODO generic sample should loop over all assets instead of hardcoding one asset like this
            string assetName = "my-http-asset";

            //TODO once schema registry client is ready, connector should register the schema on startup. The connector then puts the schema in the asset status field.
            // Additionally, the telemetry sent by this connector should be stamped as a cloud event

            try
            {
                _httpServerAssetEndpointProfile = await adrClient.GetAssetEndpointProfileAsync();

                _httpServerAsset = await adrClient.GetAssetAsync(assetName);

                if (_httpServerAsset == null)
                {
                    throw new InvalidOperationException("Missing HTTP server asset");
                }

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
                await adrClient.ObserveAssetEndpointProfileAsync(assetName);

                _logger.LogInformation("Successfully retrieved asset endpoint profile");

                TimeSpan defaultSamplingInterval = TimeSpan.FromMilliseconds(_httpServerAsset.DefaultDatasetsConfiguration!.RootElement.GetProperty("samplingInterval").GetInt16());

                // Create MQTT client from credentials provided by the operator
                MqttConnectionSettings mqttConnectionSettings = MqttConnectionSettings.FromFileMount();
                mqttConnectionSettings.TcpPort = 18883;
                _logger.LogInformation($"Connecting to MQTT broker with {mqttConnectionSettings}");

                await _sessionClient.ConnectAsync(mqttConnectionSettings);

                _logger.LogInformation($"Successfully connected to MQTT broker");

                string datasetName = _httpServerAsset.Datasets!.Keys.First();
                Dataset thermostatDataset = _httpServerAsset.Datasets![datasetName];
                TimeSpan samplingInterval = defaultSamplingInterval;
                if (thermostatDataset.DatasetConfiguration != null
                    && thermostatDataset.DatasetConfiguration.RootElement.TryGetProperty("samplingInterval", out JsonElement datasetSpecificSamplingInterval))
                {
                    samplingInterval = TimeSpan.FromMilliseconds(datasetSpecificSamplingInterval.GetInt16());
                }

                _logger.LogInformation($"Will sample dataset with name {datasetName} at a rate of once per {(int)samplingInterval.TotalMilliseconds} milliseconds");
                using Timer datasetSamplingTimer = new(SampleThermostatStatus, datasetName, 0, (int)samplingInterval.TotalMilliseconds);

                // Wait until the worker is cancelled
                await Task.Delay(-1, cancellationToken);
            }
            finally
            {
                _logger.LogInformation("Shutting down sample...");

                // TODO unimplemented so far
                //await adrClient.UnobserveAssetAsync(assetId);
                await adrClient.UnobserveAssetEndpointProfileAsync(assetName);
            }
        }

        private async void SampleThermostatStatus(object? status)
        {
            string datasetName = (string)status!;
            Dataset httpServerStatusDataset = _httpServerAsset!.Datasets![datasetName];

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

            string desiredTemperatureValue = await httpServerDesiredTemperatureDataRetriever.RetrieveDataAsync(httpServerDesiredTemperatureDataPoint.Name);
            string actualTemperatureValue = await httpServerActualTemperatureDataRetriever.RetrieveDataAsync(httpServerActualTemperatureDataPoint.Name);

            var thermostatStatus = new ThermostatStatus(desiredTemperatureValue, actualTemperatureValue);
            _logger.LogInformation($"Read thermostat status from HTTP server asset: {thermostatStatus}. Now publishing it to MQTT broker");

            var mqttMessage = new MqttApplicationMessage(httpServerStatusDataset.Topic!.Path!)
            {
                PayloadSegment = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(thermostatStatus)),
                Retain = httpServerStatusDataset.Topic.Retain == RetainHandling.Keep,
            };
            var puback = await _sessionClient.PublishAsync(mqttMessage);

            if (puback.ReasonCode != MqttClientPublishReasonCode.Success
                && puback.ReasonCode != MqttClientPublishReasonCode.NoMatchingSubscribers) // There is no consumer of these messages yet, so ignore this expected NoMatchingSubscribers error
            {
                _logger.LogInformation($"Received unsuccessful PUBACK from MQTT broker: {puback.ReasonCode} with reason {puback.ReasonString}");
            }
        }
    }
}