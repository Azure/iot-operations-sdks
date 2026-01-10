// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    public class AzureDeviceRegistryClientWrapper : IAzureDeviceRegistryClientWrapper
    {
        private readonly IAzureDeviceRegistryClient _client;
        private readonly IAssetFileMonitor _monitor;

        private const byte _dummyByte = 1;

        // The keys are the composite device names of devices that are currently being observed
        // The values are irrelevant bytes so that this concurrent dictionary can be used as a concurrent set
        private readonly ConcurrentDictionary<string, byte> _observedDevices = new();

        // The keys are the composite device names of devices that may or may not be observing some assets.
        // The values are dictionaries with keys of the asset names that are being observed and values of dummy bytes.
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _observedAssetsOnDevices = new();

        public event EventHandler<AssetChangedEventArgs>? AssetChanged;

        public event EventHandler<DeviceChangedEventArgs>? DeviceChanged;

        public AzureDeviceRegistryClientWrapper(ApplicationContext applicationContext, IMqttPubSubClient mqttPubSubClient)
        {
            _client = new AzureDeviceRegistryClient(applicationContext, mqttPubSubClient);
            _client.OnReceiveAssetUpdateEventTelemetry += AssetUpdateReceived;
            _client.OnReceiveDeviceUpdateEventTelemetry += DeviceUpdateReceived;
            _monitor = new AssetFileMonitor();
            _monitor.DeviceFileChanged += DeviceFileChanged;
            _monitor.AssetFileChanged += AssetFileChanged;
        }

        public AzureDeviceRegistryClientWrapper(IAzureDeviceRegistryClient adrServiceClient, IAssetFileMonitor? assetFileMonitor = null)
        {
            _client = adrServiceClient;
            _client.OnReceiveAssetUpdateEventTelemetry += AssetUpdateReceived;
            _client.OnReceiveDeviceUpdateEventTelemetry += DeviceUpdateReceived;
            _monitor = assetFileMonitor ?? new AssetFileMonitor();
            _monitor.DeviceFileChanged += DeviceFileChanged;
            _monitor.AssetFileChanged += AssetFileChanged;
        }

        /// <inheritdoc/>
        public void ObserveDevices()
        {
            // Any pre-existing devices will trigger the monitor's callback which triggers the ADR client to observe updates
            _monitor.ObserveDevices();
        }

        /// <inheritdoc/>
        public async Task UnobserveDevicesAsync(CancellationToken cancellationToken = default)
        {
            _monitor.UnobserveDevices();

            foreach (string compositeDeviceName in _observedDevices.Keys)
            {
                splitCompositeName(compositeDeviceName, out string deviceName, out string inboundEndpointName);
                await _client.SetNotificationPreferenceForDeviceUpdatesAsync(deviceName, inboundEndpointName, NotificationPreference.Off, null, cancellationToken);
            }

            _observedDevices.Clear();
        }

        /// <inheritdoc/>
        public void ObserveAssets(string deviceName, string inboundEndpointName)
        {
            // Any pre-existing assets will trigger the monitor's callback which triggers the ADR client to observe updates
            _monitor.ObserveAssets(deviceName, inboundEndpointName);
        }

        /// <inheritdoc/>
        public async Task UnobserveAssetsAsync(string deviceName, string inboundEndpointName, CancellationToken cancellationToken = default)
        {
            _monitor.UnobserveAssets(deviceName, inboundEndpointName);

            string compositeDeviceName = $"{deviceName}_{inboundEndpointName}";
            _observedAssetsOnDevices.TryRemove(compositeDeviceName, out ConcurrentDictionary<string, byte>? assetNames);

            if (assetNames != null)
            {
                foreach (string assetNameToUnobserve in assetNames.Keys)
                {
                    await _client.SetNotificationPreferenceForAssetUpdatesAsync(deviceName, inboundEndpointName, assetNameToUnobserve, NotificationPreference.Off, null, cancellationToken);
                }
            }
        }

        /// <inheritdoc/>
        public async Task UnobserveAllAsync(CancellationToken cancellationToken = default)
        {
            _monitor.UnobserveAll();

            foreach (string compositeDeviceName in _observedAssetsOnDevices.Keys)
            {
                foreach (string observedAssetName in _observedAssetsOnDevices[compositeDeviceName].Keys)
                {
                    splitCompositeName(compositeDeviceName, out string deviceName, out string inboundEndpointName);
                    await _client.SetNotificationPreferenceForAssetUpdatesAsync(deviceName, inboundEndpointName, observedAssetName, NotificationPreference.Off, null, cancellationToken);
                }
            }

            _observedAssetsOnDevices.Clear();

            foreach (string compositeDeviceName in _observedDevices.Keys)
            {
                splitCompositeName(compositeDeviceName, out string deviceName, out string inboundEndpointName);
                await _client.SetNotificationPreferenceForDeviceUpdatesAsync(deviceName, inboundEndpointName, NotificationPreference.Off, null, cancellationToken);
            }

            _observedDevices.Clear();
        }

        /// <inheritdoc/>
        public EndpointCredentials GetEndpointCredentials(string deviceName, string inboundEndpointName, InboundEndpointSchemaMapValue inboundEndpoint)
        {
            return _monitor.GetEndpointCredentials(deviceName, inboundEndpointName, inboundEndpoint);
        }

        /// <inheritdoc/>
        public async Task<DeviceStatus> UpdateDeviceStatusAsync(
            string deviceName,
            string inboundEndpointName,
            DeviceStatus status,
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return await _client.UpdateDeviceStatusAsync(deviceName, inboundEndpointName, status, commandTimeout, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<AssetStatus> UpdateAssetStatusAsync(
            string deviceName,
            string inboundEndpointName,
            UpdateAssetStatusRequest request,
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return await _client.UpdateAssetStatusAsync(deviceName, inboundEndpointName, request, commandTimeout, cancellationToken);
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetAssetNames(string deviceName, string inboundEndpointName)
        {
            return _monitor.GetAssetNames(deviceName, inboundEndpointName);
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetInboundEndpointNames(string deviceName)
        {
            return _monitor.GetInboundEndpointNames(deviceName);
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetDeviceNames()
        {
            return _monitor.GetDeviceNames();
        }

        /// <inheritdoc/>
        public Task<CreateOrUpdateDiscoveredAssetResponsePayload> CreateOrUpdateDiscoveredAssetAsync(string deviceName, string inboundEndpointName, CreateOrUpdateDiscoveredAssetRequest request, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return _client.CreateOrUpdateDiscoveredAssetAsync(deviceName, inboundEndpointName, request, commandTimeout, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<CreateOrUpdateDiscoveredDeviceResponsePayload> CreateOrUpdateDiscoveredDeviceAsync(CreateOrUpdateDiscoveredDeviceRequestSchema request, string inboundEndpointType, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return _client.CreateOrUpdateDiscoveredDeviceAsync(request, inboundEndpointType, commandTimeout, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<DeviceStatus> GetDeviceStatusAsync(string deviceName, string inboundEndpointName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return _client.GetDeviceStatusAsync(deviceName, inboundEndpointName, commandTimeout, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<AssetStatus> GetAssetStatusAsync(string deviceName, string inboundEndpointName, string assetName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return _client.GetAssetStatusAsync(deviceName, inboundEndpointName, assetName, commandTimeout, cancellationToken);
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            return _client.DisposeAsync();
        }

        private Task DeviceUpdateReceived(string deviceName, string inboundEndpointName, Device device)
        {
            DeviceChanged?.Invoke(this, new(deviceName, inboundEndpointName, ChangeType.Updated, device));
            return Task.CompletedTask;
        }

        private Task AssetUpdateReceived(string assetName, Asset asset)
        {
            AssetChanged?.Invoke(this, new(asset.DeviceRef.DeviceName, asset.DeviceRef.EndpointName, assetName, ChangeType.Updated, asset));
            return Task.CompletedTask;
        }

        private async void AssetFileChanged(object? sender, AssetFileChangedEventArgs e)
        {
            if (e.ChangeType == FileChangeType.Deleted)
            {
                if (_observedAssetsOnDevices.TryGetValue(e.DeviceName + "_" + e.InboundEndpointName, out ConcurrentDictionary<string, byte>? observedAssetNames)
                    && observedAssetNames!.TryRemove(e.AssetName, out _))
                {
                    // Do not set notification preference for asset updates to "off" for this asset because the ADR service no longer knows this asset. Notifications will cease automatically.
                    AssetChanged?.Invoke(this, new(e.DeviceName, e.InboundEndpointName, e.AssetName, ChangeType.Deleted, null));
                }
            }
            else if (e.ChangeType == FileChangeType.Created)
            {
                if (_observedAssetsOnDevices.TryGetValue(e.DeviceName + "_" + e.InboundEndpointName, out var observedAssetNames)
                    && observedAssetNames.ContainsKey(e.AssetName))
                {
                    // asset was already created and observed. No need to set a notification preference or notify the user
                    // about a new asset
                    return;
                }

                var notificationResponse = await _client.SetNotificationPreferenceForAssetUpdatesAsync(e.DeviceName, e.InboundEndpointName, e.AssetName, NotificationPreference.On);

                if (string.Equals(notificationResponse.ResponsePayload, "Accepted", StringComparison.InvariantCultureIgnoreCase))
                {
                    string compositeDeviceName = e.DeviceName + "_" + e.InboundEndpointName;
                    _observedAssetsOnDevices.TryAdd(compositeDeviceName, new()); // if it fails to add, then _observedAssetsOnDevices is already in the correct state

                    if (_observedAssetsOnDevices.TryGetValue(compositeDeviceName, out var assetNames))
                    {
                        if (assetNames.TryAdd(e.AssetName, _dummyByte))
                        {
                            var asset = await _client.GetAssetAsync(e.DeviceName, e.InboundEndpointName, e.AssetName);
                            AssetChanged?.Invoke(this, new(e.DeviceName, e.InboundEndpointName, e.AssetName, ChangeType.Created, asset));
                        }
                    }
                }

                //TODO what if response is negative?
            }
        }

        private async void DeviceFileChanged(object? sender, DeviceFileChangedEventArgs e)
        {
            if (e.ChangeType == FileChangeType.Deleted)
            {
                // This notes down that this device is no longer being observed
                if (_observedDevices.TryRemove(e.DeviceName + "_" + e.InboundEndpointName, out _))
                {
                    // Do not set notification preference for device updates to "off" for this device because the ADR service no longer knows this device. Notifications will cease automatically.
                    DeviceChanged?.Invoke(this, new(e.DeviceName, e.InboundEndpointName, ChangeType.Deleted, null));
                }
            }
            else if (e.ChangeType == FileChangeType.Created)
            {
                if (_observedDevices.TryGetValue(e.DeviceName + "_" + e.InboundEndpointName, out _))
                {
                    // asset was already created and observed. No need to set a notification preference or notify the user
                    // about a new asset
                    return;
                }

                var notificationResponse = await _client.SetNotificationPreferenceForDeviceUpdatesAsync(e.DeviceName, e.InboundEndpointName, NotificationPreference.On);

                if (string.Equals(notificationResponse.ResponsePayload, "Accepted", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (_observedDevices.TryAdd(e.DeviceName + "_" + e.InboundEndpointName, _dummyByte))
                    {
                        var device = await _client.GetDeviceAsync(e.DeviceName, e.InboundEndpointName);
                        DeviceChanged?.Invoke(this, new(e.DeviceName, e.InboundEndpointName, ChangeType.Created, device));
                    }
                }

                //TODO what if response is negative?
            }
        }

        // composite name follows the shape "<deviceName>_<inboundEndpointName>" where device name cannot have an underscore, but inboundEndpointName
        // may contain 0 to many underscores.
        private void splitCompositeName(string compositeName, out string deviceName, out string inboundEndpointName)
        {
            int indexOfFirstUnderscore = compositeName.IndexOf('_');
            if (indexOfFirstUnderscore == -1)
            {
                deviceName = compositeName;
                inboundEndpointName = "";
                return;
            }

            deviceName = compositeName.Substring(0, indexOfFirstUnderscore);
            inboundEndpointName = compositeName.Substring(indexOfFirstUnderscore + 1);
        }
    }
}
