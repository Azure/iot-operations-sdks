// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// A client for updating the status of an asset and for forwarding received events and/or sampled datasets.
    /// </summary>
    public class AssetClient
    {
        private readonly IAdrClientWrapper _adrClient;
        private readonly ConnectorWorker _connector;
        private readonly string _deviceName;
        private readonly string _inboundEndpointName;
        private readonly string _assetName;

        internal AssetClient(IAdrClientWrapper adrClient, string deviceName, string inboundEndpointName, string assetName, ConnectorWorker connector)
        {
            _adrClient = adrClient;
            _deviceName = deviceName;
            _inboundEndpointName = inboundEndpointName;
            _assetName = assetName;
            _connector = connector;
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
        public Task<AssetStatus> UpdateAssetStatusAsync(
            AssetStatus status,
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return _adrClient.UpdateAssetStatusAsync(
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
        public Task<AssetStatus> GetAssetStatusAsync(
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return _adrClient.GetAssetStatusAsync(
                _deviceName,
                _inboundEndpointName,
                _assetName,
                commandTimeout,
                cancellationToken);
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
            await _connector.ForwardSampledDatasetAsync(_deviceName, _inboundEndpointName, _assetName, dataset, serializedPayload, userData, cancellationToken);
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
            await _connector.ForwardReceivedEventAsync(_deviceName, _inboundEndpointName, _assetName, eventGroupName, assetEvent, serializedPayload, userData, cancellationToken);
        }
    }
}
