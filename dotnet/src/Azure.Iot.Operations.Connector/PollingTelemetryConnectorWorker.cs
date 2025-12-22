// Copyright(c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Microsoft.Extensions.Logging;

namespace Azure.Iot.Operations.Connector
{
    public class PollingTelemetryConnectorWorker : ConnectorWorker
    {
        private readonly Dictionary<string, Dictionary<string, Timer>> _assetsSamplingTimers = new();
        private readonly IDatasetSamplerFactory _datasetSamplerFactory;

        public PollingTelemetryConnectorWorker(ApplicationContext applicationContext, ILogger<ConnectorWorker> logger, IMqttClient mqttClient, IDatasetSamplerFactory datasetSamplerFactory, IMessageSchemaProvider messageSchemaFactory, IAzureDeviceRegistryClientWrapperProvider adrClientFactory, IConnectorLeaderElectionConfigurationProvider? leaderElectionConfigurationProvider = null) : base(applicationContext, logger, mqttClient, messageSchemaFactory, adrClientFactory, leaderElectionConfigurationProvider)
        {
            base.WhileDeviceIsAvailable = WhileDeviceAvailableAsync;
            base.WhileAssetIsAvailable = WhileAssetAvailableAsync;
            _datasetSamplerFactory = datasetSamplerFactory;
        }

        public async Task WhileDeviceAvailableAsync(DeviceAvailableEventArgs args, CancellationToken cancellationToken)
        {
            try
            {
                // Report device status is okay
                _logger.LogInformation("Reporting device status as okay to Azure Device Registry service...");
                await args.DeviceEndpointClient.GetAndUpdateDeviceStatusAsync((currentDeviceStatus) => {
                    currentDeviceStatus.Config ??= new();
                    currentDeviceStatus.Config.LastTransitionTime = DateTime.UtcNow;
                    currentDeviceStatus.Config.Error = null;
                    return currentDeviceStatus;
                }, true, null, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to report device status to Azure Device Registry service");
            }
        }

        public async Task WhileAssetAvailableAsync(AssetAvailableEventArgs args, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (args.Asset.Datasets == null)
            {
                return;
            }

            Dictionary<string, Timer> datasetsTimers = new();
            _assetsSamplingTimers[args.AssetName] = datasetsTimers;
            foreach (AssetDataset dataset in args.Asset.Datasets!)
            {
                EndpointCredentials? credentials = null;
                if (args.Device.Endpoints != null
                    && args.Device.Endpoints.Inbound != null
                    && args.Device.Endpoints.Inbound.TryGetValue(args.InboundEndpointName, out var inboundEndpoint))
                {
                    credentials = _adrClient!.GetEndpointCredentials(args.DeviceName, args.InboundEndpointName, inboundEndpoint);
                }

                IDatasetSampler datasetSampler = _datasetSamplerFactory.CreateDatasetSampler(args.DeviceName, args.Device, args.InboundEndpointName, args.AssetName, args.Asset, dataset, credentials);

                TimeSpan samplingInterval = await datasetSampler.GetSamplingIntervalAsync(dataset);

                _logger.LogInformation("Dataset with name {0} in asset with name {1} will be sampled once every {2} milliseconds", dataset.Name, args.AssetName, samplingInterval.TotalMilliseconds);

                var datasetSamplingTimer = new Timer(async (state) =>
                {
                    byte[] sampledData;
                    try
                    {
                        sampledData = await datasetSampler.SampleDatasetAsync(dataset);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to sample the dataset");

                        try
                        {
                            await args.DeviceEndpointClient.GetAndUpdateDeviceStatusAsync((currentDeviceStatus) => {
                                currentDeviceStatus.Config ??= new ConfigStatus();
                                currentDeviceStatus.Config.Error =
                                    new ConfigError()
                                    {
                                        Message = $"Unable to sample the device. Error message: {e.Message}",
                                    };
                                currentDeviceStatus.Config.LastTransitionTime = DateTime.UtcNow;
                                return currentDeviceStatus;
                            }, true, null, cancellationToken);
                        }
                        catch (Exception e2)
                        {
                            _logger.LogError(e2, "Failed to report device status to Azure Device Registry service");
                        }
                        return;
                    }

                    try
                    {
                        await args.AssetClient.ForwardSampledDatasetAsync(dataset, sampledData);

                        try
                        {
                            // The dataset was sampled as expected, so report the asset status as okay
                            _logger.LogInformation("Reporting asset status as okay to Azure Device Registry service...");
                            await args.AssetClient.GetAndUpdateAssetStatusAsync(
                                (currentAssetStatus) =>
                                {
                                    currentAssetStatus.Config ??= new();
                                    currentAssetStatus.Config.Error = null;
                                    currentAssetStatus.Config.LastTransitionTime = DateTime.UtcNow;
                                    currentAssetStatus.UpdateDatasetStatus(new AssetDatasetEventStreamStatus()
                                    {
                                        Name = dataset.Name,
                                        MessageSchemaReference = args.AssetClient.GetRegisteredDatasetMessageSchema(dataset.Name),
                                        Error = null
                                    });
                                    return currentAssetStatus;
                                },
                                true,
                                null,
                                cancellationToken);
                        }
                        catch (Exception e2)
                        {
                            _logger.LogError(e2, "Failed to report asset status to Azure Device Registry service");
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to forward the sampled dataset");

                        try
                        {
                            await args.DeviceEndpointClient.GetAndUpdateDeviceStatusAsync((currentDeviceStatus) => {
                                currentDeviceStatus.Config ??= new ConfigStatus();
                                currentDeviceStatus.Config.Error =
                                    new ConfigError()
                                    {
                                        Message = $"Unable to sample the device. Error message: {e.Message}",
                                    };
                                currentDeviceStatus.Config.LastTransitionTime = DateTime.UtcNow;
                                return currentDeviceStatus;
                            }, true, null, cancellationToken);
                        }
                        catch (Exception e2)
                        {
                            _logger.LogError(e2, "Failed to report device status to Azure Device Registry service");
                        }
                    }

                    try
                    {
                        // No errors were encountered while sampling or forwarding data for this device, so clear any error status
                        // it may have had previously
                        _logger.LogInformation("Reporting device status as okay to Azure Device Registry service...");
                        await args.DeviceEndpointClient.GetAndUpdateDeviceStatusAsync((currentDeviceStatus) => {
                            currentDeviceStatus.Config ??= new();
                            currentDeviceStatus.Config.LastTransitionTime = DateTime.UtcNow;
                            currentDeviceStatus.Config.Error = null;
                            return currentDeviceStatus;
                        }, true, null, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to report device status to Azure Device Registry service");
                    }
                }, null, TimeSpan.FromSeconds(0), samplingInterval);

                if (!datasetsTimers.TryAdd(dataset.Name, datasetSamplingTimer))
                {
                    _logger.LogError("Failed to save dataset sampling timer for asset with name {} for dataset with name {}", args.AssetName, dataset.Name);
                }
            }

            // Waits until the asset is no longer available
            cancellationToken.WaitHandle.WaitOne();

            // Stop sampling all datasets in this asset now that the asset is unavailable
            foreach (AssetDataset dataset in args.Asset.Datasets!)
            {
                _logger.LogInformation("Dataset with name {0} in asset with name {1} will no longer be periodically sampled", dataset.Name, args.AssetName);
                _assetsSamplingTimers[args.AssetName][dataset.Name].Dispose();
            }
        }
    }
}
