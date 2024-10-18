// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using GenericHttpServerConnectorWorkerService;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.GenericHttpConnectorSample
{
    public class GenericHttpConnectorWorkerService : BackgroundService
    {
        private readonly ILogger<GenericHttpConnectorWorkerService> _logger;
        private MqttSessionClient _sessionClient;
        private IHttpServerDatasetSampler _httpServerDatasetSampler;

        private Asset? _httpServerAsset;
        private AssetEndpointProfile? _httpServerAssetEndpointProfile;

        public GenericHttpConnectorWorkerService(ILogger<GenericHttpConnectorWorkerService> logger, MqttSessionClient mqttSessionClient, IHttpServerDatasetSampler httpServerDatasetSampler)
        {
            _logger = logger;
            _sessionClient = mqttSessionClient;
            _httpServerDatasetSampler = httpServerDatasetSampler;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            AzureDeviceRegistryClient adrClient = new();
            _logger.LogInformation("Successfully created ADR client");

            string assetId = "todo - doesn't matter yet";

            //TODO once schema registry client is ready, connector should register the schema on startup. The connector then puts the schema in the asset status field.
            // Additionally, the telemetry sent by this connector should be stamped as a cloud event

            try
            {
                _httpServerAssetEndpointProfile = await adrClient.GetAssetEndpointProfileAsync(assetId);

                _httpServerAsset = await adrClient.GetAssetAsync(assetId);

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
                mqttConnectionSettings.TcpPort = 18883;
                _logger.LogInformation($"Connecting to MQTT broker with {mqttConnectionSettings}");

                await _sessionClient.ConnectAsync(mqttConnectionSettings);

                _logger.LogInformation($"Successfully connected to MQTT broker");

                foreach (string datasetName in _httpServerAsset.Datasets!.Keys)
                {
                    Dataset thermostatDataset = _httpServerAsset.Datasets![datasetName];
                    TimeSpan samplingInterval = defaultSamplingInterval;
                    if (thermostatDataset.DatasetConfiguration != null
                        && thermostatDataset.DatasetConfiguration.RootElement.TryGetProperty("samplingInterval", out JsonElement datasetSpecificSamplingInterval))
                    {
                        samplingInterval = TimeSpan.FromMilliseconds(datasetSpecificSamplingInterval.GetInt16());
                    }

                    _logger.LogInformation($"Will sample dataset with name {datasetName} at a rate of once per {(int)samplingInterval.TotalMilliseconds} milliseconds");
                    using Timer datasetSamplingTimer = new(SampleDataset, datasetName, 0, (int)samplingInterval.TotalMilliseconds);
                }

                // Wait until the worker is cancelled
                await Task.Delay(-1, cancellationToken);
            }
            finally
            {
                _logger.LogInformation("Shutting down sample...");

                await adrClient.UnobserveAssetAsync(assetId);
                await adrClient.UnobserveAssetEndpointProfileAsync(assetId);
            }
        }

        private async void SampleDataset(object? status)
        {
            string datasetName = (string)status!;
            Dataset httpServerDataset = _httpServerAsset!.Datasets![datasetName];

            string httpServerUsername = _httpServerAssetEndpointProfile!.Credentials!.Username!;
            byte[] httpServerPassword = _httpServerAssetEndpointProfile.Credentials!.Password!;

            foreach (DataPoint httpServerDataPoint in httpServerDataset.DataPoints!)
            {
                HttpMethod httpServerHttpMethod = HttpMethod.Parse(httpServerDataPoint.DataPointConfiguration!.RootElement.GetProperty("HttpRequestMethod").GetString());
                var httpRequestContext = 
                    new HttpRequestContext(
                        _httpServerAssetEndpointProfile.TargetAddress, 
                        httpServerHttpMethod,
                        httpServerDataPoint.DataSource!, 
                        httpServerDataPoint.Name, 
                        httpServerUsername, 
                        httpServerPassword);

                await _httpServerDatasetSampler.SampleAsync(httpRequestContext, datasetName);
            }

            byte[] serializedPayload = _httpServerDatasetSampler.GetSerializedDatasetPayload(datasetName);
            _logger.LogInformation($"Read thermostat status from HTTP server asset. Now publishing it to MQTT broker");

            var mqttMessage = new MqttApplicationMessage(httpServerDataset.Topic!.Path!)
            {
                PayloadSegment = serializedPayload,
                Retain = httpServerDataset.Topic.Retain == RetainHandling.Keep,
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