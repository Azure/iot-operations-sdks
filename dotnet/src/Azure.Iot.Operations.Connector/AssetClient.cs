// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
        private readonly SemaphoreSlim _semaphore = new(0, 1);

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
        public async Task<AssetStatus> GetAndUpdateAssetStatusAsync(Func<AssetStatus, AssetStatus?> handler, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                AssetStatus currentStatus = await GetAssetStatusAsync(commandTimeout, cancellationToken);
                AssetStatus? desiredStatus = handler.Invoke(currentStatus);
                if (desiredStatus != null)
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
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ForwardSampledDatasetAsync(AssetDataset dataset, byte[] serializedPayload, Dictionary<string, string>? userData = null, CancellationToken cancellationToken = default)
        {
            await _connector.ForwardSampledDatasetAsync(_deviceName, _device, _inboundEndpointName, _assetName, _asset, dataset, serializedPayload, userData, cancellationToken);
        }

        /// <summary>
        /// Push a received event payload to the configured destinations.
        /// </summary>
        /// <param name="eventGroupName">The name of the event group that this event belongs to.</param>
        /// <param name="assetEvent">The event.</param>
        /// <param name="serializedPayload">The payload to push to the configured destinations.</param>
        /// <param name="userData">Optional headers to include in the telemetry. Only applicable for datasets with a destination of the MQTT broker.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ForwardReceivedEventAsync(string eventGroupName, AssetEvent assetEvent, byte[] serializedPayload, Dictionary<string, string>? userData = null, CancellationToken cancellationToken = default)
        {
            await _connector.ForwardReceivedEventAsync(_deviceName, _device, _inboundEndpointName, _assetName, _asset, eventGroupName, assetEvent, serializedPayload, userData, cancellationToken);
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
                // It's fine if this sempahore is already disposed.
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
    }
}
