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
    public class ConnectorWorker : BackgroundService
    {
        private readonly ILogger<ConnectorWorker> _logger;
        private MqttSessionClient _sessionClient;
        private IDatasetSamplerFactory _datasetSamplerFactory;
        private ConcurrentDictionary<string, IDatasetSampler> _datasetSamplers = new();

        // Mapping of asset name to the dictionary that maps a dataset name to its sampler
        private Dictionary<string, Dictionary<string, Timer>> _samplers = new();

        public ConnectorWorker(
            ILogger<ConnectorWorker> logger, 
            MqttSessionClient mqttSessionClient, 
            IDatasetSamplerFactory datasetSamplerFactory)
        {
            _logger = logger;
            _sessionClient = mqttSessionClient;
            _datasetSamplerFactory = datasetSamplerFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            //TODO do active passive LE in the template level. Check replica count > 1 in connector config works as expected
            string candidateName = Guid.NewGuid().ToString();
            bool isLeader = false;

            // Create MQTT client from credentials provided by the operator
            MqttConnectionSettings mqttConnectionSettings = MqttConnectionSettings.FromFileMount();
            mqttConnectionSettings.ClientId = Guid.NewGuid().ToString(); //TODO get from config
            _logger.LogInformation($"Connecting to MQTT broker with {mqttConnectionSettings}");

            //TODO retry if it fails, but wait until what to try again? Just rely on retry policy?
            await _sessionClient.ConnectAsync(mqttConnectionSettings, cancellationToken);

            _logger.LogInformation($"Successfully connected to MQTT broker");

            bool doingLeaderElection = false;
            TimeSpan leaderElectionTermLength = TimeSpan.FromSeconds(5);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        AssetMonitor assetMonitor = new AssetMonitor();

                        TaskCompletionSource aepDeletedOrUpdatedTcs = new();
                        TaskCompletionSource<AssetEndpointProfile> aepCreatedTcs = new();
                        assetMonitor.AssetEndpointProfileChanged += (sender, args) =>
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
                                    aepCreatedTcs.TrySetResult(args.AssetEndpointProfile);
                                }
                            }
                            else
                            {
                                aepDeletedOrUpdatedTcs.TrySetResult();
                            }
                        };

                        assetMonitor.ObserveAssetEndpointProfile(null, cancellationToken);

                        _logger.LogInformation("Waiting for asset endpoint profile to be discovered");
                        AssetEndpointProfile assetEndpointProfile = await aepCreatedTcs.Task.WaitAsync(cancellationToken);

                        _logger.LogInformation("Successfully discovered the asset endpoint profile");

                        if (assetEndpointProfile.AdditionalConfiguration != null)
                        {
                            _logger.LogInformation("####1");
                            _logger.LogInformation($"{assetEndpointProfile.AdditionalConfiguration.ToString()}")
                        }

                        if (assetEndpointProfile.AdditionalConfiguration.RootElement.TryGetProperty("leadershipPositionId", out JsonElement value))
                        {
                            _logger.LogInformation("####2");
                        }

                        if (value.ValueKind == JsonValueKind.String)
                        {
                            _logger.LogInformation("####3");
                        }

                        if (value.GetString() != null)
                        {
                            _logger.LogInformation("####4");
                        }


                        if (assetEndpointProfile.AdditionalConfiguration != null
                            && assetEndpointProfile.AdditionalConfiguration.RootElement.TryGetProperty("leadershipPositionId", out value)
                            && value.ValueKind == JsonValueKind.String
                            && value.GetString() != null)
                        {
                            doingLeaderElection = true;
                            string leadershipPositionId = value.GetString()!;

                            _logger.LogInformation($"Leadership position Id {leadershipPositionId} was configured, so this pod will perform leader election");

                            await using LeaderElectionClient leaderElectionClient = new(_sessionClient, leadershipPositionId, candidateName);

                            leaderElectionClient.AutomaticRenewalOptions = new LeaderElectionAutomaticRenewalOptions()
                            {
                                AutomaticRenewal = true,
                                ElectionTerm = leaderElectionTermLength,
                                RenewalPeriod = leaderElectionTermLength.Subtract(TimeSpan.FromSeconds(1))
                            };

                            leaderElectionClient.LeadershipChangeEventReceivedAsync += (sender, args) =>
                            {
                                isLeader = args.NewLeader != null && args.NewLeader.GetString().Equals(candidateName);
                                if (isLeader)
                                {
                                    _logger.LogInformation("Received notification that this pod is the leader");
                                }

                                return Task.CompletedTask;
                            };

                            //TODO how does this work when the DSS store shouldn't be touched? There is no way to know for sure if you are still leader without
                            //polling. Maybe it is fine if there is some overlap with 2 pods active for (campaign-length) amount of time?
                            _logger.LogInformation("This pod is waiting to be elected leader.");
                            await leaderElectionClient.CampaignAsync(leaderElectionTermLength);
                            
                            _logger.LogInformation("This pod was elected leader.");
                        }

                        assetMonitor.AssetChanged += (sender, args) =>
                        {
                            _logger.LogInformation($"Recieved a notification an asset with name {args.AssetName} has been {args.ChangeType.ToString().ToLower()}.");

                            if (args.ChangeType == ChangeType.Deleted)
                            {
                                StopSamplingAsset(args.AssetName);
                            }
                            else if (args.ChangeType == ChangeType.Created)
                            {
                                StartSamplingAsset(assetEndpointProfile, args.Asset!, cancellationToken);
                            }
                            else
                            {
                                // asset changes don't all necessitate re-creating the relevant dataset samplers, but there is no way to know
                                // at this level what changes are dataset-specific nor which of those changes require a new sampler. Because
                                // of that, this sample just assumes all asset changes require the factory requesting a new sampler.
                                StopSamplingAsset(args.AssetName);
                                StartSamplingAsset(assetEndpointProfile, args.Asset!, cancellationToken);
                            }
                        };

                        _logger.LogInformation("Now monitoring for asset creation/deletion/updates");
                        assetMonitor.ObserveAssets(null, cancellationToken);

                        // Wait until the worker is cancelled or it is no longer the leader
                        while (!cancellationToken.IsCancellationRequested && (isLeader || !doingLeaderElection) && !aepDeletedOrUpdatedTcs.Task.IsCompleted)
                        {
                            try
                            {
                                if (doingLeaderElection)
                                {
                                    await Task.WhenAny(
                                        aepDeletedOrUpdatedTcs.Task,
                                        Task.Delay(leaderElectionTermLength)).WaitAsync(cancellationToken);
                                }
                                else
                                {
                                    await Task.WhenAny(
                                        aepDeletedOrUpdatedTcs.Task).WaitAsync(cancellationToken);

                                }
                            }
                            catch (OperationCanceledException)
                            { 
                                // expected outcome, allow the while loop to check status again
                            }
                        }

                        if (cancellationToken.IsCancellationRequested)
                        {
                            _logger.LogInformation("This pod is shutting down. It will now stop monitoring and sampling assets.");
                        }
                        else if (aepDeletedOrUpdatedTcs.Task.IsCompleted)
                        { 
                            _logger.LogInformation("Received a notification that the asset endpoint profile has changed. This pod will now cancel current asset sampling and restart monitoring assets.");
                        }
                        else
                        {
                            _logger.LogInformation("This pod is no longer the leader. It will now stop monitoring and sampling assets.");
                        }

                        foreach (Dictionary<string, Timer> datasetSamplers in _samplers.Values)
                        {
                            foreach (Timer datasetSampler in datasetSamplers.Values)
                            {
                                datasetSampler.Dispose();
                            }
                        }

                        _samplers.Clear();
                        assetMonitor.UnobserveAssets();
                        assetMonitor.UnobserveAssetEndpointProfile();
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

        private void StartSamplingAsset(AssetEndpointProfile assetEndpointProfile, Asset asset, CancellationToken cancellationToken = default)
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
                    Timer datasetSamplingTimer = new(SampleDataset, new DatasetSamplerContext(assetEndpointProfile, asset, datasetName), 0, (int)samplingInterval.TotalMilliseconds);
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
                _datasetSamplers.TryAdd(datasetName, _datasetSamplerFactory.CreateDatasetSampler(samplerContext.AssetEndpointProfile, asset, dataset));
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