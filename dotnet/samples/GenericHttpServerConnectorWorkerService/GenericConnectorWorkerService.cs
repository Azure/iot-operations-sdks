// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.GenericHttpConnectorSample
{
    public class GenericConnectorWorkerService : BackgroundService
    {
        private readonly ILogger<GenericConnectorWorkerService> _logger;
        private MqttSessionClient _sessionClient;
        private IDatasetSampler _datasetSampler;

        private Dictionary<string, Asset> _assets = new();
        private AssetEndpointProfile? _assetEndpointProfile;

        public GenericConnectorWorkerService(ILogger<GenericConnectorWorkerService> logger, MqttSessionClient mqttSessionClient, IDatasetSampler datasetSampler)
        {
            _logger = logger;
            _sessionClient = mqttSessionClient;
            _datasetSampler = datasetSampler;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            AzureDeviceRegistryClient adrClient = new();
            _logger.LogInformation("Successfully created ADR client");

            //TODO once schema registry client is ready, connector should register the schema on startup. The connector then puts the schema in the asset status field.
            // Additionally, the telemetry sent by this connector should be stamped as a cloud event

            try
            {
                _assetEndpointProfile = await adrClient.GetAssetEndpointProfileAsync(cancellationToken);

                adrClient.AssetEndpointProfileChanged += (sender, newAssetEndpointProfile) =>
                {
                    _logger.LogInformation("Recieved a notification that the asset endpoint definition has changed.");
                    _assetEndpointProfile = newAssetEndpointProfile;
                };

                await adrClient.ObserveAssetEndpointProfileAsync(null, cancellationToken);

                _logger.LogInformation("Successfully retrieved asset endpoint profile");

                // Create MQTT client from credentials provided by the operator
                MqttConnectionSettings mqttConnectionSettings = MqttConnectionSettings.FromFileMount();
                mqttConnectionSettings.TcpPort = 18883;
                _logger.LogInformation($"Connecting to MQTT broker with {mqttConnectionSettings}");

                await _sessionClient.ConnectAsync(mqttConnectionSettings, cancellationToken);

                _logger.LogInformation($"Successfully connected to MQTT broker");

                foreach (string assetName in await adrClient.GetAssetNamesAsync(cancellationToken))
                {
                    _logger.LogInformation($"Discovered asset with name {assetName}");
                    Asset? asset = await adrClient.GetAssetAsync(assetName, cancellationToken);

                    if (asset == null)
                    {
                        continue;
                    }

                    _assets.Add(assetName, asset);

                    adrClient.AssetChanged += (sender, newAsset) =>
                    {

                        if (newAsset == null)
                        {
                            _logger.LogInformation($"Recieved a notification the asset with name {assetName} has been deleted.");
                            _assets.Remove(assetName);
                        }
                        else
                        {
                            _logger.LogInformation($"Recieved a notification the asset with name {assetName} has been updated.");
                            _assets[assetName] = newAsset;
                        }
                    };

                    await adrClient.ObserveAssetAsync(assetName, null, cancellationToken);

                    foreach (string datasetName in _assets[assetName].DatasetsDictionary!.Keys)
                    {
                        Dataset dataset = _assets[assetName].DatasetsDictionary![datasetName];

                        TimeSpan defaultSamplingInterval = TimeSpan.FromMilliseconds(_assets[assetName].DefaultDatasetsConfiguration!.RootElement.GetProperty("samplingInterval").GetInt16());

                        TimeSpan samplingInterval = defaultSamplingInterval;
                        if (dataset.DatasetConfiguration != null
                            && dataset.DatasetConfiguration.RootElement.TryGetProperty("samplingInterval", out JsonElement datasetSpecificSamplingInterval))
                        {
                            samplingInterval = TimeSpan.FromMilliseconds(datasetSpecificSamplingInterval.GetInt16());
                        }

                        _logger.LogInformation($"Will sample dataset with name {datasetName} at a rate of once per {(int)samplingInterval.TotalMilliseconds} milliseconds");
                        using Timer datasetSamplingTimer = new(SampleDataset, new SamplerContext(assetName, datasetName), 0, (int)samplingInterval.TotalMilliseconds);
                    }
                }

                // Wait until the worker is cancelled
                await Task.Delay(-1, cancellationToken);
            }
            finally
            {
                _logger.LogInformation("Shutting down sample...");

                foreach (string assetName in _assets.Keys)
                {
                    await adrClient.UnobserveAssetAsync(assetName);
                }

                await adrClient.UnobserveAssetEndpointProfileAsync();
            }
        }

        private async void SampleDataset(object? status)
        {
            SamplerContext samplerContext = (SamplerContext)status!;

            string assetName = samplerContext.AssetName;
            string datasetName = samplerContext.DatasetName;

            Asset? asset = _assets[assetName];
            if (asset == null)
            {
                _logger.LogInformation($"Asset with name {assetName} was deleted. This sample won't sample its data.");
                return;
            }

            Dictionary<string, Dataset>? assetDatasets = asset.DatasetsDictionary;
            if (assetDatasets == null || !assetDatasets.ContainsKey(datasetName))
            {
                _logger.LogInformation($"Dataset with name {datasetName} in asset with name {samplerContext.AssetName} was deleted. This sample won't sample this dataset anymore.");
                return;
            }

            Dataset dataset = assetDatasets[datasetName];

            byte[] serializedPayload = await _datasetSampler.SampleAsync(_assetEndpointProfile!, dataset);

            _logger.LogInformation($"Read dataset from asset. Now publishing it to MQTT broker");

            var mqttMessage = new MqttApplicationMessage(dataset.Topic!.Path!)
            {
                PayloadSegment = serializedPayload,
                Retain = dataset.Topic.Retain == RetainHandling.Keep,
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