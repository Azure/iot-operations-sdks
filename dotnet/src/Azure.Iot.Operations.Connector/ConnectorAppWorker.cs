using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.Assets;
using Azure.Iot.Operations.Services.LeaderElection;
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
            //TODO do active passive LE in the template level. Check replica count > 1 in connector config works as expected
            string candidateName = Guid.NewGuid().ToString();
            bool isLeader = false;

            // Create MQTT client from credentials provided by the operator
            MqttConnectionSettings mqttConnectionSettings = MqttConnectionSettings.FromFileMount();
            _logger.LogInformation($"Connecting to MQTT broker with {mqttConnectionSettings}");

            //TODO retry if it fails, but wait until what to try again? Just rely on retry policy?
            await _sessionClient.ConnectAsync(mqttConnectionSettings, cancellationToken);

            _logger.LogInformation($"Successfully connected to MQTT broker");

            await using LeaderElectionClient leaderElectionClient = new(_sessionClient, _leadershipPositionId, candidateName);

            TimeSpan leaderElectionTermLength = TimeSpan.FromSeconds(5);
            /*leaderElectionClient.AutomaticRenewalOptions = new LeaderElectionAutomaticRenewalOptions()
            {
                AutomaticRenewal = true,
                ElectionTerm = leaderElectionTermLength,
                RenewalPeriod = leaderElectionTermLength.Subtract(TimeSpan.FromSeconds(1))
            };*/

            leaderElectionClient.LeadershipChangeEventReceivedAsync += (sender, args) =>
            {
                isLeader = args.NewLeader != null && args.NewLeader.GetString().Equals(candidateName);
                if (isLeader)
                {
                    _logger.LogInformation("Received notification that this pod is the leader");
                }
                else
                {
                    _logger.LogInformation("Received notification that this pod is not the leader");
                }
                return Task.CompletedTask;
            };

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

                        TaskCompletionSource<AssetEndpointProfile> aepTcs = new();
                        _adrClient.AssetEndpointProfileChanged += (sender, args) =>
                        {
                            // Each connector should have one AEP deployed to the pod. It shouldn't ever be deleted, but it may be updated.
                            if (args.ChangeType == ChangeType.Created)
                            {
                                if (args.AssetEndpointProfile == null)
                                {
                                    // shouldn't ever happen
                                    _logger.LogError("Received notification that asset endpoint profile was created, but no asset endpoint profile was provided");
                                }
                                else
                                {
                                    aepTcs.TrySetResult(args.AssetEndpointProfile);
                                }
                            }

                            //TODO upon AEP updated, just re-create all samplers? Stick this whole function in a loop. Would need to re-create the asset monitor
                            // so that it starts with no assets saved?

                            _assetEndpointProfile = args.AssetEndpointProfile;
                        };

                        _adrClient.ObserveAssetEndpointProfile(null, cancellationToken);

                        _logger.LogInformation("Waiting for asset endpoint profile to be discovered");
                        await aepTcs.Task.WaitAsync(cancellationToken);

                        _logger.LogInformation("Successfully retrieved asset endpoint profile");

                        _adrClient.AssetChanged += (sender, args) =>
                        {
                            _logger.LogInformation($"Recieved a notification an asset with name {args.AssetName} has been {args.ChangeType.ToString().ToLower()}.");

                            if (args.ChangeType == ChangeType.Deleted)
                            {
                                StopSamplingAsset(args.AssetName);
                            }
                            else if (args.ChangeType == ChangeType.Created)
                            {
                                StartSamplingAsset(args.Asset!, cancellationToken);
                            }
                            else
                            {
                                // asset changes don't all necessitate re-creating the relevant dataset samplers, but there is no way to know
                                // at this level what changes are dataset-specific nor which of those changes require a new sampler. Because
                                // of that, this sample just assumes all asset changes require the factory requesting a new sampler.
                                StopSamplingAsset(args.AssetName);
                                StartSamplingAsset(args.Asset!, cancellationToken);
                            }
                        };

                        _logger.LogInformation("Now monitoring for asset creation/deletion/updates");
                        _adrClient.ObserveAssets(null, cancellationToken);

                        // Wait until the worker is cancelled or it is no longer the leader
                        while (!cancellationToken.IsCancellationRequested && isLeader)
                        {
                            await Task.Delay(leaderElectionTermLength);
                        }

                        _logger.LogInformation("Pod is either shutting down or is no longer the leader. It will now stop monitoring and sampling assets.");

                        foreach (Dictionary<string, Timer> datasetSamplers in _samplers.Values)
                        {
                            foreach (Timer datasetSampler in datasetSamplers.Values)
                            {
                                datasetSampler.Dispose();
                            }
                        }

                        _samplers.Clear();
                        _adrClient.UnobserveAssets();
                        _adrClient.UnobserveAssetEndpointProfile();
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