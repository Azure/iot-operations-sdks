// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// A client for updating the status of an asset and for forwarding received events and/or sampled datasets.
    /// </summary>
    public class AssetClient : IDisposable
    {
        private readonly IAzureDeviceRegistryClientWrapper _adrClient;
        private readonly ConnectorWorker _connector;
        private readonly string _deviceName;
        private readonly string _inboundEndpointName;
        private readonly string _assetName;
        private readonly Device _device;
        private readonly Asset _asset;

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

        public void Dispose()
        {
            try
            {
                _semaphore.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // It's fine if this semaphore is already disposed.
            }
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

        /// <summary>
        /// Report the health of a given asset dataset.
        /// </summary>
        /// <param name="telemetry">The health status to report.</param>
        /// <param name="qos">The MQTT quality of service to send this report with.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ReportDatasetRuntimeHealthEvent(List<DatasetsSchemaElementSchema> telemetry, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            await _adrClient.ReportDatasetRuntimeHealthEvent(
                _deviceName,
                _inboundEndpointName,
                new()
                {
                    DatasetRuntimeHealthEvent = new()
                    {
                        AssetName = _assetName,
                        Datasets = telemetry,
                    }
                },
                qos,
                telemetryTimeout,
                cancellationToken);
        }

        /// <summary>
        /// Report the health of a given asset dataset.
        /// </summary>
        /// <param name="telemetry">The health status to report.</param>
        /// <param name="qos">The MQTT quality of service to send this report with.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ReportDatasetRuntimeHealthEvent(DatasetsSchemaElementSchema telemetry, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            await ReportDatasetRuntimeHealthEvent(new List<DatasetsSchemaElementSchema>() { telemetry }, qos, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report the health of a given asset event.
        /// </summary>
        /// <param name="telemetry">The health status to report.</param>
        /// <param name="qos">The MQTT quality of service to send this report with.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ReportEventRuntimeHealthEvent(List<EventsSchemaElementSchema> telemetry, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            await _adrClient.ReportEventRuntimeHealthEvent(
                _deviceName,
                _inboundEndpointName,
                new()
                {
                    EventRuntimeHealthEvent = new()
                    {
                        AssetName = _assetName,
                        Events = telemetry,
                    }
                },
                qos,
                telemetryTimeout,
                cancellationToken);
        }

        /// <summary>
        /// Report the health of a given asset event.
        /// </summary>
        /// <param name="telemetry">The health status to report.</param>
        /// <param name="qos">The MQTT quality of service to send this report with.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ReportEventRuntimeHealthEvent(EventsSchemaElementSchema telemetry, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            await ReportEventRuntimeHealthEvent(new List<EventsSchemaElementSchema>() { telemetry }, qos, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report the health of a given asset stream.
        /// </summary>
        /// <param name="telemetry">The health status to report.</param>
        /// <param name="qos">The MQTT quality of service to send this report with.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ReportStreamRuntimeHealthEvent(List<StreamsSchemaElementSchema> telemetry, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            await _adrClient.ReportStreamRuntimeHealthEvent(
                _deviceName,
                _inboundEndpointName,
                new()
                {
                    StreamRuntimeHealthEvent = new()
                    {
                        AssetName = _assetName,
                        Streams = telemetry,
                    }
                },
                qos,
                telemetryTimeout,
                cancellationToken);
        }

        /// <summary>
        /// Report the health of a given asset stream.
        /// </summary>
        /// <param name="telemetry">The health status to report.</param>
        /// <param name="qos">The MQTT quality of service to send this report with.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ReportStreamRuntimeHealthEvent(StreamsSchemaElementSchema telemetry, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            await ReportStreamRuntimeHealthEvent(new List<StreamsSchemaElementSchema>() { telemetry }, qos, telemetryTimeout, cancellationToken);
        }

        /// <summary>
        /// Report the health of a given management action.
        /// </summary>
        /// <param name="telemetry">The health status to report.</param>
        /// <param name="qos">The MQTT quality of service to send this report with.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ReportManagementActionRuntimeHealthEvent(List<ManagementActionsSchemaElementSchema> telemetry, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            await _adrClient.ReportManagementActionRuntimeHealthEvent(
                _deviceName,
                _inboundEndpointName,
                new()
                {
                    ManagementActionRuntimeHealthEvent = new()
                    {
                        AssetName = _assetName,
                        ManagementActions = telemetry,
                    }
                },
                qos,
                telemetryTimeout,
                cancellationToken);
        }

        /// <summary>
        /// Report the health of a given management action.
        /// </summary>
        /// <param name="telemetry">The health status to report.</param>
        /// <param name="qos">The MQTT quality of service to send this report with.</param>
        /// <param name="telemetryTimeout">Optional message expiry time for the telemetry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ReportManagementActionRuntimeHealthEvent(ManagementActionsSchemaElementSchema telemetry, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            await ReportManagementActionRuntimeHealthEvent(new List<ManagementActionsSchemaElementSchema>() { telemetry }, qos, telemetryTimeout, cancellationToken);
        }
    }
}
