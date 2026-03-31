// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// A client for updating the status of an asset and for forwarding received events and/or sampled datasets.
    /// </summary>
    public class AssetClient : IAsyncDisposable
    {
        private readonly IAzureDeviceRegistryClientWrapper _adrClient;
        private readonly ConnectorWorker _connector;
        private readonly string _deviceName;
        private readonly string _inboundEndpointName;
        private readonly string _assetName;
        private readonly Device _device;
        private readonly Asset _asset;
        private readonly AssetHealthStatusReporter _healthReporter;

        // Used to make getAndUpdate calls behave atomically so that a user does not accidentally update
        // an asset while another thread is in the middle of a getAndUpdate call.
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        internal AssetClient(IAzureDeviceRegistryClientWrapper adrClient, string deviceName, string inboundEndpointName, string assetName, ConnectorWorker connector, Device device, Asset asset)
        {
            _adrClient = adrClient;
            _deviceName = deviceName;
            _inboundEndpointName = inboundEndpointName;
            _assetName = assetName;
            _connector = connector;
            _device = device;
            _asset = asset;
            _healthReporter = new(adrClient.GetWrapped(), deviceName, inboundEndpointName, assetName, TimeSpan.FromSeconds(10)); //TODO timespan param somewhere? Maybe a AssetClient.CreateHealthStatusReporter API?
        }

        /// <summary>
        /// Get the current status of this asset and then optionally update it.
        /// </summary>
        /// <param name="handler">The function that determines the new asset status when given the current asset status.</param>
        /// <param name="onlyIfChanged">
        /// Only send the status update if the new status is different from the current status. If the only
        /// difference between the current and new status is a 'LastTransitionTime' field, then the statuses will be
        /// considered identical.
        /// </param>
        /// <param name="commandTimeout">The timeout for each of the 'get' and 'update' commands.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The latest asset status after this operation.</returns>
        /// <remarks>
        /// If after retrieving the current status, you don't want to send any updates, <paramref name="handler"/> should return null.
        /// If this happens, this function will return the latest asset status without trying to update it.
        ///
        /// This method uses a semaphore to ensure that this same client doesn't accidentally update the asset status while
        /// another thread is in the middle of updating the same asset. This ensures that the current device status provided in <paramref name="handler"/>
        /// stays accurate while any updating occurs.
        /// </remarks>
        public async Task<AssetStatus> GetAndUpdateAssetStatusAsync(Func<AssetStatus, AssetStatus?> handler, bool onlyIfChanged = false, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                AssetStatus currentStatus = await GetAssetStatusAsync(commandTimeout, cancellationToken);
                AssetStatus? desiredStatus = handler.Invoke(currentStatus);
                if (desiredStatus != null && (!onlyIfChanged || currentStatus.EqualTo(desiredStatus)))
                {
                    return await UpdateAssetStatusAsync(desiredStatus, commandTimeout, cancellationToken);
                }

                return currentStatus;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Push a sampled dataset to the configured destinations.
        /// </summary>
        /// <param name="dataset">The dataset that was sampled.</param>
        /// <param name="serializedPayload">The payload to push to the configured destinations.</param>
        /// <param name="userData">Optional headers to include in the telemetry. Only applicable for datasets with a destination of the MQTT broker.</param>
        /// <param name="protocolSpecificIdentifier">Optional protocol specific identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ForwardSampledDatasetAsync(AssetDataset dataset, byte[] serializedPayload, Dictionary<string, string>? userData = null, string? protocolSpecificIdentifier = null, CancellationToken cancellationToken = default)
        {
            await _connector.ForwardSampledDatasetAsync(_deviceName, _device, _inboundEndpointName, _assetName, _asset, dataset, serializedPayload, userData, protocolSpecificIdentifier, cancellationToken);
        }

        /// <summary>
        /// Push a received event payload to the configured destinations.
        /// </summary>
        /// <param name="eventGroupName">The name of the event group that this event belongs to.</param>
        /// <param name="assetEvent">The event.</param>
        /// <param name="serializedPayload">The payload to push to the configured destinations.</param>
        /// <param name="userData">Optional headers to include in the telemetry. Only applicable for datasets with a destination of the MQTT broker.</param>
        /// <param name="protocolSpecificIdentifier">Optional protocol specific identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ForwardReceivedEventAsync(string eventGroupName, AssetEvent assetEvent, byte[] serializedPayload, Dictionary<string, string>? userData = null, string? protocolSpecificIdentifier = null, CancellationToken cancellationToken = default)
        {
            await _connector.ForwardReceivedEventAsync(_deviceName, _device, _inboundEndpointName, _assetName, _asset, eventGroupName, assetEvent, serializedPayload, userData, protocolSpecificIdentifier, cancellationToken);
        }

        public MessageSchemaReference? GetRegisteredDatasetMessageSchema(string datasetName)
        {
            return _connector.GetRegisteredDatasetMessageSchema(_deviceName, _inboundEndpointName, _assetName, datasetName);
        }

        public MessageSchemaReference? GetRegisteredEventMessageSchema(string eventGroupName, string eventName)
        {
            return _connector.GetRegisteredEventMessageSchema(_deviceName, _inboundEndpointName, _assetName, eventGroupName, eventName);
        }

        /// <summary>
        /// Report the health of a given asset's datasets.
        /// </summary>
        /// <param name="runtimeHealth">The health status to report.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ReportDatasetRuntimeHealthAsync(List<ConnectorDatasetsRuntimeHealthEvent> runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            List<DatasetsRuntimeHealthEvent> servicesRuntimeHealths = new();
            foreach (ConnectorDatasetsRuntimeHealthEvent connectorRuntimeHealth in runtimeHealth)
            {
                servicesRuntimeHealths.Add(new DatasetsRuntimeHealthEvent()
                {
                    DatasetName = connectorRuntimeHealth.DatasetName,
                    RuntimeHealth = new()
                    {
                        LastUpdateTime = DateTime.UtcNow,
                        Message = connectorRuntimeHealth.RuntimeHealth.Message,
                        ReasonCode = connectorRuntimeHealth.RuntimeHealth.ReasonCode,
                        Status = connectorRuntimeHealth.RuntimeHealth.Status,
                        Version = _asset.Version ?? 0
                    }
                });
            }

            await _healthReporter.ReportDatasetHealthStatusAsync(servicesRuntimeHealths, telemetryTimeout, cancellationToken);
        }

        public async Task ReportDatasetRuntimeHealthAsync(string datasetName, ConnectorRuntimeHealth runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            var datasetsRuntimeHealthEvent = new ConnectorDatasetsRuntimeHealthEvent()
            {
                DatasetName = datasetName,
                RuntimeHealth = runtimeHealth,
            };

            await ReportDatasetRuntimeHealthAsync(new List<ConnectorDatasetsRuntimeHealthEvent>() { datasetsRuntimeHealthEvent }, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report the health of a given asset's events.
        /// </summary>
        /// <param name="runtimeHealth">The health status to report.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ReportEventRuntimeHealthAsync(List<ConnectorEventsRuntimeHealthEvent> runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            List<EventsRuntimeHealthEvent> servicesRuntimeHealths = new();
            foreach (ConnectorEventsRuntimeHealthEvent connectorRuntimeHealth in runtimeHealth)
            {
                servicesRuntimeHealths.Add(new EventsRuntimeHealthEvent()
                {
                    EventGroupName = connectorRuntimeHealth.EventGroupName,
                    EventName = connectorRuntimeHealth.EventName,
                    RuntimeHealth = new()
                    {
                        LastUpdateTime = DateTime.UtcNow,
                        Message = connectorRuntimeHealth.RuntimeHealth.Message,
                        ReasonCode = connectorRuntimeHealth.RuntimeHealth.ReasonCode,
                        Status = connectorRuntimeHealth.RuntimeHealth.Status,
                        Version = _asset.Version ?? 0
                    }
                });
            }
            await _healthReporter.ReportEventHealthStatusAsync(servicesRuntimeHealths, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report the health of a given asset's event.
        /// </summary>
        /// <param name="runtimeHealth">The health status to report.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ReportEventRuntimeHealthAsync(string eventGroupName, string eventName, ConnectorRuntimeHealth runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            ConnectorEventsRuntimeHealthEvent eventsRuntimeHealthEvent = new ConnectorEventsRuntimeHealthEvent()
            {
                EventGroupName = eventGroupName,
                EventName = eventName,
                RuntimeHealth = runtimeHealth,
            };

            //TODO need to add some caching at this layer such that not every report is sent (when nothing has changed) prior to
            //actually releasing this feature.
            await ReportEventRuntimeHealthAsync(new List<ConnectorEventsRuntimeHealthEvent>() { eventsRuntimeHealthEvent }, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report the health of a given asset's streams.
        /// </summary>
        /// <param name="runtimeHealth">The health status to report.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ReportStreamRuntimeHealthAsync(List<ConnectorStreamsRuntimeHealthEvent> runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            List<StreamsRuntimeHealthEvent> servicesRuntimeHealths = new();
            foreach (ConnectorStreamsRuntimeHealthEvent connectorRuntimeHealth in runtimeHealth)
            {
                servicesRuntimeHealths.Add(new StreamsRuntimeHealthEvent()
                {
                    StreamName = connectorRuntimeHealth.StreamName,
                    RuntimeHealth = new()
                    {
                        LastUpdateTime = DateTime.UtcNow,
                        Message = connectorRuntimeHealth.RuntimeHealth.Message,
                        ReasonCode = connectorRuntimeHealth.RuntimeHealth.ReasonCode,
                        Status = connectorRuntimeHealth.RuntimeHealth.Status,
                        Version = _asset.Version ?? 0
                    }
                });
            }

            await _healthReporter.ReportStreamHealthStatusAsync(servicesRuntimeHealths, telemetryTimeout, cancellationToken);
        }

        public async Task ReportStreamRuntimeHealthAsync(string streamName, ConnectorRuntimeHealth runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            ConnectorStreamsRuntimeHealthEvent streamsRuntimeHealthEvent = new()
            {
                StreamName = streamName,
                RuntimeHealth = runtimeHealth,
            };

            await ReportStreamRuntimeHealthAsync(new List<ConnectorStreamsRuntimeHealthEvent>() { streamsRuntimeHealthEvent }, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report the health of some given management actions.
        /// </summary>
        /// <param name="runtimeHealth">The health status to report.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ReportManagementActionRuntimeHealthAsync(List<ConnectorManagementActionsRuntimeHealthEvent> runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            List<ManagementActionsRuntimeHealthEvent> servicesRuntimeHealths = new();
            foreach (ConnectorManagementActionsRuntimeHealthEvent connectorRuntimeHealth in runtimeHealth)
            {
                servicesRuntimeHealths.Add(new ManagementActionsRuntimeHealthEvent()
                {
                    ManagementGroupName = connectorRuntimeHealth.ManagementGroupName,
                    ManagementActionName = connectorRuntimeHealth.ManagementActionName,
                    RuntimeHealth = new()
                    {
                        LastUpdateTime = DateTime.UtcNow,
                        Message = connectorRuntimeHealth.RuntimeHealth.Message,
                        ReasonCode = connectorRuntimeHealth.RuntimeHealth.ReasonCode,
                        Status = connectorRuntimeHealth.RuntimeHealth.Status,
                        Version = _asset.Version ?? 0
                    }
                });
            }

            await _healthReporter.ReportManagementActionHealthStatusAsync(servicesRuntimeHealths, telemetryTimeout, cancellationToken);
        }

        public async Task ReportManagementActionRuntimeHealthAsync(string managementGroupName, string managementActionName, ConnectorRuntimeHealth runtimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            ConnectorManagementActionsRuntimeHealthEvent managementActionsRuntimeHealthEvent = new()
            {
                ManagementGroupName = managementGroupName,
                ManagementActionName= managementActionName,
                RuntimeHealth = runtimeHealth,
            };

            await ReportManagementActionRuntimeHealthAsync(new List<ConnectorManagementActionsRuntimeHealthEvent>() { managementActionsRuntimeHealthEvent }, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Update the status of this asset in the Azure Device Registry service
        /// </summary>
        /// <param name="status">The status of this asset and its datasets/event groups/streams/management groups</param>
        /// <param name="commandTimeout">The timeout for this RPC command invocation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The status returned by the Azure Device Registry service</returns>
        /// <remarks>
        /// This update behaves like a 'put' in that it will replace all current state for this asset in the Azure
        /// Device Registry service with what is provided.
        /// </remarks>
        private async Task<AssetStatus> UpdateAssetStatusAsync(
            AssetStatus status,
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return await _adrClient.UpdateAssetStatusAsync(
                _deviceName,
                _inboundEndpointName,
                new UpdateAssetStatusRequest()
                {
                    AssetName = _assetName,
                    AssetStatus = status,
                },
                commandTimeout,
                cancellationToken);
        }

        /// <summary>
        /// Get the status of this asset from the Azure Device Registry service
        /// </summary>
        /// <param name="commandTimeout">The timeout for this RPC command invocation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The status returned by the Azure Device Registry service</returns>
        private async Task<AssetStatus> GetAssetStatusAsync(
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return await _adrClient.GetAssetStatusAsync(
                _deviceName,
                _inboundEndpointName,
                _assetName,
                commandTimeout,
                cancellationToken);
        }

        public virtual async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            GC.SuppressFinalize(this);
        }

        public virtual async ValueTask DisposeAsync(bool disposing)
        {
            await DisposeAsyncCore();
        }

        private async ValueTask DisposeAsyncCore()
        {
            try
            {
                _semaphore.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // It's fine if this semaphore is already disposed.
            }

            try
            {
                await _healthReporter.CancelHealthStatusReportingAsync();
                await _healthReporter.DisposeAsync();
            }
            catch (ObjectDisposedException)
            {
                // It's fine if this is already disposed.
            }
        }
    }
}
