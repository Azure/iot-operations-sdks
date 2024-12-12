using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.Assets;
using Azure.Iot.Operations.Services.LeaderElection;
using Azure.Iot.Operations.Services.SchemaRegistry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using SchemaInfo = Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1.Object_Ms_Adr_SchemaRegistry_Schema__1;

namespace Azure.Iot.Operations.Connector
{
    public class TelemetryConnectorWorker : BackgroundService
    {
        private readonly ILogger<TelemetryConnectorWorker> _logger;
        private IMqttClient _mqttClient;
        private IDatasetSamplerFactory _datasetSamplerFactory;
        private IAssetMonitor _assetMonitor;
        private ConcurrentDictionary<string, IDatasetSampler> _datasetSamplers = new();

        // Mapping of asset name to the dictionary that maps a dataset name to its sampler
        private Dictionary<string, Dictionary<string, Timer>> _samplers = new();

        public TelemetryConnectorWorker(
            ILogger<TelemetryConnectorWorker> logger,
            IMqttClient mqttClient, 
            IDatasetSamplerFactory datasetSamplerFactory,
            IAssetMonitor assetMonitor)
        {
            _logger = logger;
            _mqttClient = mqttClient;
            _datasetSamplerFactory = datasetSamplerFactory;
            _assetMonitor = assetMonitor;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            string candidateName = Guid.NewGuid().ToString();
            bool isLeader = false;

            // Create MQTT client from credentials provided by the operator
            MqttConnectionSettings mqttConnectionSettings = MqttConnectionSettings.FromFileMount();
            _logger.LogInformation($"Connecting to MQTT broker with {mqttConnectionSettings}");

            //TODO retry if it fails, but wait until what to try again? Just rely on retry policy?
            //TODO can't rely on retry if user isn't using session client? Just force session client here?
            await _mqttClient.ConnectAsync(mqttConnectionSettings, cancellationToken);

            _logger.LogInformation($"Successfully connected to MQTT broker");

            bool doingLeaderElection = false;
            TimeSpan leaderElectionTermLength = TimeSpan.FromSeconds(5);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        TaskCompletionSource aepDeletedOrUpdatedTcs = new();
                        TaskCompletionSource<AssetEndpointProfile> aepCreatedTcs = new();
                        _assetMonitor.AssetEndpointProfileChanged += (sender, args) =>
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

                        _assetMonitor.ObserveAssetEndpointProfile(null, cancellationToken);

                        _logger.LogInformation("Waiting for asset endpoint profile to be discovered");
                        AssetEndpointProfile assetEndpointProfile = await aepCreatedTcs.Task.WaitAsync(cancellationToken);

                        _logger.LogInformation("Successfully discovered the asset endpoint profile");

                        if (assetEndpointProfile.AdditionalConfiguration != null
                            && assetEndpointProfile.AdditionalConfiguration.RootElement.TryGetProperty("leadershipPositionId", out JsonElement value)
                            && value.ValueKind == JsonValueKind.String
                            && value.GetString() != null)
                        {
                            doingLeaderElection = true;
                            string leadershipPositionId = value.GetString()!;

                            _logger.LogInformation($"Leadership position Id {leadershipPositionId} was configured, so this pod will perform leader election");

                            await using LeaderElectionClient leaderElectionClient = new(_mqttClient, leadershipPositionId, candidateName);

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

                        _assetMonitor.AssetChanged += (sender, args) =>
                        {
                            _logger.LogInformation($"Recieved a notification an asset with name {args.AssetName} has been {args.ChangeType.ToString().ToLower()}.");

                            if (args.ChangeType == ChangeType.Deleted)
                            {
                                StopSamplingAsset(args.AssetName);
                            }
                            else if (args.ChangeType == ChangeType.Created)
                            {
                                StartSamplingAsset(assetEndpointProfile, args.Asset!, args.AssetName, cancellationToken);
                            }
                            else
                            {
                                // asset changes don't all necessitate re-creating the relevant dataset samplers, but there is no way to know
                                // at this level what changes are dataset-specific nor which of those changes require a new sampler. Because
                                // of that, this sample just assumes all asset changes require the factory requesting a new sampler.
                                StopSamplingAsset(args.AssetName);
                                StartSamplingAsset(assetEndpointProfile, args.Asset!, args.AssetName, cancellationToken);
                            }
                        };

                        _logger.LogInformation("Now monitoring for asset creation/deletion/updates");
                        _assetMonitor.ObserveAssets(null, cancellationToken);

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
                        else if (doingLeaderElection)
                        {
                            _logger.LogInformation("This pod is no longer the leader. It will now stop monitoring and sampling assets.");
                        }
                        else
                        {
                            // Shouldn't happen. The pod should either be cancelled, the AEP should have changed, or this pod should have lost its position as leader
                            _logger.LogInformation("This pod will now cancel current asset sampling and restart monitoring assets.");
                        }

                        foreach (Dictionary<string, Timer> datasetSamplers in _samplers.Values)
                        {
                            foreach (Timer datasetSampler in datasetSamplers.Values)
                            {
                                datasetSampler.Dispose();
                            }
                        }

                        _samplers.Clear();
                        _assetMonitor.UnobserveAssets();
                        _assetMonitor.UnobserveAssetEndpointProfile();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Encountered an error: {ex}");
                    }
                }
            }
            finally
            {
                _logger.LogInformation("Shutting down the connector");
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

        private void StartSamplingAsset(AssetEndpointProfile assetEndpointProfile, Asset asset, string assetName, CancellationToken cancellationToken = default)
        {
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

                    TimeSpan samplingInterval;
                    if (dataset.DatasetConfiguration != null
                        && dataset.DatasetConfiguration.RootElement.TryGetProperty("samplingInterval", out JsonElement datasetSpecificSamplingInterval)
                        && datasetSpecificSamplingInterval.TryGetInt32(out int datasetSpecificSamplingIntervalMilliseconds))
                    {
                        samplingInterval = TimeSpan.FromMilliseconds(datasetSpecificSamplingIntervalMilliseconds);
                    }
                    else if (asset.DefaultDatasetsConfiguration != null
                        && asset.DefaultDatasetsConfiguration.RootElement.TryGetProperty("samplingInterval", out JsonElement defaultDatasetSamplingInterval)
                        && defaultDatasetSamplingInterval.TryGetInt32(out int defaultSamplingIntervalMilliseconds))
                    {
                        samplingInterval = TimeSpan.FromMilliseconds(defaultSamplingIntervalMilliseconds);
                    }
                    else
                    {
                        _logger.LogError($"Dataset with name {datasetName} in Asset with name {assetName} has no configured sampling interval. This dataset will not be sampled.");
                        return;
                    }

                    _logger.LogInformation($"Will sample dataset with name {datasetName} on asset with name {assetName} at a rate of once per {(int)samplingInterval.TotalMilliseconds} milliseconds");
                    Timer datasetSamplingTimer = new(SampleDataset, new DatasetSamplerContext(assetEndpointProfile, asset, assetName, datasetName, cancellationToken), 0, (int)samplingInterval.TotalMilliseconds);
                    _samplers[assetName][datasetName] = datasetSamplingTimer;
                }
            }
        }

        private async void SampleDataset(object? status)
        {
            DatasetSamplerContext samplerContext = (DatasetSamplerContext)status!;

            Asset asset = samplerContext.Asset;
            string datasetName = samplerContext.DatasetName;
            string assetName = samplerContext.AssetName;

            Dictionary<string, Dataset>? assetDatasets = asset.DatasetsDictionary;
            if (assetDatasets == null || !assetDatasets.ContainsKey(datasetName))
            {
                _logger.LogInformation($"Dataset with name {datasetName} in asset with name {assetName} was deleted. This sample won't sample this dataset anymore.");
                return;
            }

            Dataset dataset = assetDatasets[datasetName];

            IDatasetSampler? datasetSampler;
            if (!_datasetSamplers.ContainsKey(datasetName))
            {
                datasetSampler = _datasetSamplerFactory.CreateDatasetSampler(samplerContext.AssetEndpointProfile, asset, dataset);
                _datasetSamplers.TryAdd(datasetName, datasetSampler);

                //TODO what if message schema changes, but name stays the same?
                //SchemaInfo messageSchema = await datasetSampler.GetMessageSchemaAsync(dataset);
                //await using SchemaRegistryClient schemaRegistryClient = new(_mqttClient);

            }

            if (!_datasetSamplers.TryGetValue(datasetName, out datasetSampler))
            {
                _logger.LogInformation($"Dataset with name {datasetName} in asset with name {assetName} was deleted. This sample won't sample this dataset anymore.");
                return;
            }

            byte[] serializedPayload;
            try
            {
                serializedPayload = await datasetSampler.SampleDatasetAsync(dataset);
            }
            catch (Exception e)
            { 
                _logger.LogError(e, $"Error sampling dataset with name {datasetName} in asset with name {assetName}");
                return;
            }

            _logger.LogInformation($"Read dataset with name {dataset.Name} from asset with name {assetName}. Now publishing it to MQTT broker: {Encoding.UTF8.GetString(serializedPayload)}");

            Topic topic;
            if (dataset.Topic != null)
            {
                topic = dataset.Topic;
            }
            else if (asset.DefaultTopic != null)
            {
                topic = asset.DefaultTopic;
            }
            else
            {
                _logger.LogInformation($"Dataset with name {datasetName} in asset with name {assetName} has no configured MQTT topic to publish to. This sample won't publish the data sampled from the asset.");
                return;
            }

            var mqttMessage = new MqttApplicationMessage(topic.Path)
            {
                PayloadSegment = serializedPayload,
                Retain = topic.Retain == RetainHandling.Keep,
            };

            var puback = await _mqttClient.PublishAsync(mqttMessage);

            if (puback.ReasonCode == MqttClientPublishReasonCode.Success
                || puback.ReasonCode == MqttClientPublishReasonCode.NoMatchingSubscribers)
            {
                // NoMatchingSubscribers case is still successful in the sense that the PUBLISH packet was delivered to the broker successfully.
                // It does suggest that the broker has no one to send that PUBLISH packet to, though.
                _logger.LogInformation($"Message was accepted by the MQTT broker with PUBACK reason code: {puback.ReasonCode} and reason {puback.ReasonString}");
            }
            else
            {
                _logger.LogInformation($"Received unsuccessful PUBACK from MQTT broker: {puback.ReasonCode} with reason {puback.ReasonString}");
            }
        }
    }
}