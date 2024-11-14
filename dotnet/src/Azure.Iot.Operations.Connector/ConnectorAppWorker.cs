using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.Assets;
using Azure.Iot.Operations.Services.LeaderElection;
using Azure.Iot.Operations.Services.StateStore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.Connector
{
    public class ConnectorAppWorker : BackgroundService
    {
        private readonly ILogger<ConnectorAppWorker> _logger;
        private MqttSessionClient _sessionClient;
        private IDatasetSamplerFactory _datasetSamplerFactory;
        private ConcurrentDictionary<string, IDatasetSampler> _datasetSamplers = new();
        private AssetMonitor _adrClient;
        private string _leadershipPositionId;

        private AssetEndpointProfile? _assetEndpointProfile;

        // Mapping of asset name to the dictionary that maps a dataset name to its sampler
        private Dictionary<string, Dictionary<string, Timer>> _samplers = new();

        public ConnectorAppWorker(
            ILogger<ConnectorAppWorker> logger, 
            MqttSessionClient mqttSessionClient, 
            IDatasetSamplerFactory datasetSamplerFactory,
            string leadershipPositionId)
        {
            _logger = logger;
            _sessionClient = mqttSessionClient;
            _datasetSamplerFactory = datasetSamplerFactory;
            _adrClient = new();
            _leadershipPositionId = leadershipPositionId;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Create MQTT client from credentials provided by the operator
            MqttConnectionSettings mqttConnectionSettings = MqttConnectionSettings.FromFileMount();
            _logger.LogInformation($"Connecting to MQTT broker with {mqttConnectionSettings}");

            //TODO retry if it fails, but wait until what to try again? Just rely on retry policy?
            await _sessionClient.ConnectAsync(mqttConnectionSettings, cancellationToken);

            await using StateStoreClient stateStoreClient = new(_sessionClient);

            try
            {
                await stateStoreClient.SetAsync(_leadershipPositionId, "someValue");
                _logger.LogError("Successfully set the leadership key");
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to set the leadership key");
                _logger.LogError(e.Message);
                _logger.LogError(e.StackTrace);
            }

            try
            {
                await stateStoreClient.ObserveAsync(_leadershipPositionId);
                _logger.LogError("Successfully observed the leadership key");
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to observe the leadership key");
                _logger.LogError(e.Message); 
                _logger.LogError(e.StackTrace);
            }

            try
            {
                await stateStoreClient.UnobserveAsync(_leadershipPositionId);
                _logger.LogError("Successfully unobserved the leadership key");
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to unobserve the leadership key");
                _logger.LogError(e.Message);
                _logger.LogError(e.StackTrace);
            }

            //TODO do active passive LE in the template level. Check replica count > 1 in connector config works as expected
            string candidateName = Guid.NewGuid().ToString();
            bool isLeader = false;



            _logger.LogInformation($"Successfully connected to MQTT broker");

            await using LeaderElectionClient leaderElectionClient = new(_sessionClient, _leadershipPositionId, candidateName);

            TimeSpan leaderElectionTermLength = TimeSpan.FromSeconds(5);
            /*leaderElectionClient.AutomaticRenewalOptions = new LeaderElectionAutomaticRenewalOptions()
            {
                AutomaticRenewal = true,
                ElectionTerm = leaderElectionTermLength,
                RenewalPeriod = leaderElectionTermLength.Subtract(TimeSpan.FromSeconds(1))
            };*/
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        //TODO how does this work when the DSS store shouldn't be touched? There is no way to know for sure if you are still leader without
                        //polling. Maybe it is fine if there is some overlap with 2 pods active for (campaign-length) amount of time?
                        _logger.LogInformation("This pod is waiting to be elected leader.");
                        await leaderElectionClient.CampaignAsync(leaderElectionTermLength);

                        _logger.LogInformation("This pod was elected leader.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Encountered an error: {ex}");
                    }
                }
            }
            finally
            {
                _logger.LogInformation("Shutting down sample...");
            }
        }

        private void StopSamplingAsset(string assetName)
        {
            // Stop sampling this asset since it was deleted
            foreach (Timer datasetSampler in _samplers[assetName].Values)
            {
                datasetSampler.Dispose();
            }

            _samplers.Remove(assetName);
        }

        private void StartSamplingAsset(Asset asset, CancellationToken cancellationToken = default)
        {
            string assetName = asset.DisplayName!;

            _samplers[assetName] = new();
            if (asset.DatasetsDictionary == null)
            {
                _logger.LogInformation($"Asset with name {assetName} has no datasets to sample");
                return;
            }
            else
            { 
                foreach (string datasetName in asset.DatasetsDictionary!.Keys)
                {
                    Dataset dataset = asset.DatasetsDictionary![datasetName];

                    TimeSpan defaultSamplingInterval = TimeSpan.FromMilliseconds(asset.DefaultDatasetsConfiguration!.RootElement.GetProperty("samplingInterval").GetInt16());

                    TimeSpan samplingInterval = defaultSamplingInterval;
                    if (dataset.DatasetConfiguration != null
                        && dataset.DatasetConfiguration.RootElement.TryGetProperty("samplingInterval", out JsonElement datasetSpecificSamplingInterval))
                    {
                        samplingInterval = TimeSpan.FromMilliseconds(datasetSpecificSamplingInterval.GetInt16());
                    }

                    _logger.LogInformation($"Will sample dataset with name {datasetName} on asset with name {assetName} at a rate of once per {(int)samplingInterval.TotalMilliseconds} milliseconds");
                    Timer datasetSamplingTimer = new(SampleDataset, new DatasetSamplerContext(asset, datasetName), 0, (int)samplingInterval.TotalMilliseconds);
                    _samplers[assetName][datasetName] = datasetSamplingTimer;

                    string mqttMessageSchema = dataset.GetMqttMessageSchema();
                    _logger.LogInformation($"Derived the schema for dataset with name {datasetName} in asset with name {assetName}:");
                    _logger.LogInformation(mqttMessageSchema);

                    //TODO register the message schema with the schema registry service
                }
            }
        }

        private async void SampleDataset(object? status)
        {
            DatasetSamplerContext samplerContext = (DatasetSamplerContext)status!;

            Asset asset = samplerContext.Asset;
            string datasetName = samplerContext.DatasetName;

            Dictionary<string, Dataset>? assetDatasets = asset.DatasetsDictionary;
            if (assetDatasets == null || !assetDatasets.ContainsKey(datasetName))
            {
                _logger.LogInformation($"Dataset with name {datasetName} in asset with name {samplerContext.Asset.DisplayName} was deleted. This sample won't sample this dataset anymore.");
                return;
            }

            Dataset dataset = assetDatasets[datasetName];

            if (!_datasetSamplers.ContainsKey(datasetName))
            {
                _datasetSamplers.TryAdd(datasetName, _datasetSamplerFactory.CreateDatasetSampler(_assetEndpointProfile!, asset, dataset));
            }

            if (!_datasetSamplers.TryGetValue(datasetName, out IDatasetSampler? datasetSampler))
            {
                _logger.LogInformation($"Dataset with name {datasetName} in asset with name {samplerContext.Asset.DisplayName} was deleted. This sample won't sample this dataset anymore.");
                return;
            }

            byte[] serializedPayload = await datasetSampler.SampleDatasetAsync(dataset);

            _logger.LogInformation($"Read dataset with name {dataset.Name} from asset with name {samplerContext.Asset.DisplayName}. Now publishing it to MQTT broker: {Encoding.UTF8.GetString(serializedPayload)}");

            var topic = dataset.Topic != null ? dataset.Topic! : asset.DefaultTopic!;
            var mqttMessage = new MqttApplicationMessage(topic.Path!)
            {
                PayloadSegment = serializedPayload,
                Retain = topic.Retain == RetainHandling.Keep,
            };

            var puback = await _sessionClient.PublishAsync(mqttMessage);

            if (puback.ReasonCode == MqttClientPublishReasonCode.Success)
            {
                _logger.LogInformation($"Received successful PUBACK from MQTT broker: {puback.ReasonCode} with reason {puback.ReasonString}");
            }
            else
            {
                // There is no consumer of these messages yet, so NoMatchingSubscribers error is expected here
                _logger.LogInformation($"Received unsuccessful PUBACK from MQTT broker: {puback.ReasonCode} with reason {puback.ReasonString}");
            }
        }
    }
}