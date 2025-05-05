// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Azure.Iot.Operations.Connector.Assets;
using System.Collections.Concurrent;

namespace Azure.Iot.Operations.Connector
{
    public class AdrClientWrapper : IAdrClientWrapper // TODO naming
    {
        private readonly IAdrServiceClient _client;
        private readonly IAssetFileMonitor _monitor;

        private const byte _dummyByte = 1;
        private readonly ConcurrentDictionary<string, byte> _observedDevices = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _observedAssets = new();

        public event EventHandler<AssetChangedEventArgs>? AssetChanged;

        public event EventHandler<DeviceChangedEventArgs>? DeviceChanged;

        public AdrClientWrapper(ApplicationContext applicationContext, IMqttPubSubClient mqttPubSubClient, string connectorClientId)
        {
            _client = new AdrServiceClient(applicationContext, mqttPubSubClient, connectorClientId);
            _client.OnReceiveAssetUpdateEventTelemetry += AssetUpdateReceived;
            _client.OnReceiveDeviceUpdateEventTelemetry += DeviceUpdateReceived;
            _monitor = new AssetFileMonitor();
            _monitor.DeviceFileChanged += DeviceFileChanged;
            _monitor.AssetFileChanged += AssetFileChanged;
        }

        /// </inheritdoc>
        public void ObserveDevices()
        {
            // Any pre-existing devices will trigger the monitor's callback which triggers the ADR client to observe updates
            _monitor.ObserveDevices();
        }

        /// </inheritdoc>
        public async Task UnobserveDevicesAsync(CancellationToken cancellationToken = default)
        {
            _monitor.UnobserveDevices();

            foreach (string compositeDeviceName in _observedDevices.Keys)
            {
                string deviceName = compositeDeviceName.Split('_')[0];
                string inboundEndpointName = compositeDeviceName.Split('_')[1];
                await _client.UnobserveDeviceEndpointUpdatesAsync(deviceName, inboundEndpointName, null, cancellationToken);
            }

            _observedDevices.Clear();
        }

        /// </inheritdoc>
        public void ObserveAssets(string deviceName, string inboundEndpointName)
        {
            // Any pre-existing assets will trigger the monitor's callback which triggers the ADR client to observe updates
            _monitor.ObserveAssets(deviceName, inboundEndpointName);
        }

        /// </inheritdoc>
        public async Task UnobserveAssetsAsync(string deviceName, string inboundEndpointName, CancellationToken cancellationToken = default)
        {
            _monitor.UnobserveAssets(deviceName, inboundEndpointName);

            string compositeDeviceName = $"{deviceName}_{inboundEndpointName}";
            _observedAssets.Remove(compositeDeviceName, out HashSet<string>? assetNames);

            if (assetNames != null)
            {
                foreach (string assetNameToUnobserve in assetNames)
                {
                    await _client.UnobserveAssetUpdatesAsync(deviceName, inboundEndpointName, assetNameToUnobserve, null, cancellationToken);
                }
            }
        }

        /// </inheritdoc>
        public async Task UnobserveAllAsync(CancellationToken cancellationToken = default)
        {
            _monitor.UnobserveAll();

            foreach (string compositeDeviceName in _observedAssets.Keys)
            {
                foreach (string observedAssetName in _observedAssets[compositeDeviceName])
                {
                    string deviceName = compositeDeviceName.Split('_')[0];
                    string inboundEndpointName = compositeDeviceName.Split('_')[1];
                    await _client.UnobserveAssetUpdatesAsync(deviceName, inboundEndpointName, observedAssetName, null, cancellationToken);
                }
            }

            _observedAssets.Clear();

            foreach (string compositeDeviceName in _observedDevices.Keys)
            {
                string deviceName = compositeDeviceName.Split('_')[0];
                string inboundEndpointName = compositeDeviceName.Split('_')[1];
                await _client.UnobserveDeviceEndpointUpdatesAsync(deviceName, inboundEndpointName, null, cancellationToken);
            }

            _observedDevices.Clear();
        }

        /// </inheritdoc>
        public EndpointCredentials GetEndpointCredentials(InboundEndpointSchemaMapValue inboundEndpoint)
        {
            return _monitor.GetEndpointCredentials(inboundEndpoint);
        }

        /// </inheritdoc>
        public Task<Device> UpdateDeviceStatusAsync(
            string deviceName,
            string inboundEndpointName,
            DeviceStatus status,
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return _client.UpdateDeviceStatusAsync(deviceName, inboundEndpointName, status, commandTimeout, cancellationToken);
        }

        /// </inheritdoc>
        public Task<Asset> UpdateAssetStatusAsync(
            string deviceName,
            string inboundEndpointName,
            UpdateAssetStatusRequest request,
            TimeSpan? commandTimeout = null,
            CancellationToken cancellationToken = default)
        {
            return _client.UpdateAssetStatusAsync(deviceName, inboundEndpointName, request, commandTimeout, cancellationToken);
        }

        /// </inheritdoc>
        public IEnumerable<string> GetAssetNames(string deviceName, string inboundEndpointName)
        {
            return _monitor.GetAssetNames(deviceName, inboundEndpointName);
        }

        /// </inheritdoc>
        public IEnumerable<string> GetInboundEndpointNames(string deviceName)
        {
            return _monitor.GetInboundEndpointNames(deviceName);
        }

        /// </inheritdoc>
        public IEnumerable<string> GetDeviceNames()
        {
            return _monitor.GetDeviceNames();
        }

        private Task DeviceUpdateReceived(string compositeDeviceName, Device device)
        {
            string deviceName = compositeDeviceName.Split('_')[0];
            string inboundEndpointName = compositeDeviceName.Split('_')[1];
            DeviceChanged?.Invoke(this, new(deviceName, inboundEndpointName, ChangeType.Updated, device));
            return Task.CompletedTask;
        }

        private Task AssetUpdateReceived(string assetName, Asset asset)
        {
            AssetChanged?.Invoke(this, new(asset.Specification.DeviceRef.DeviceName, asset.Specification.DeviceRef.EndpointName, asset.Name, ChangeType.Updated, asset));
            return Task.CompletedTask;
        }

        private async void AssetFileChanged(object? sender, Assets.AssetChangedEventArgs e)
        {
            if (e.ChangeType == AssetFileMonitorChangeType.Deleted)
            {
                await _client.UnobserveAssetUpdatesAsync(e.DeviceName, e.InboundEndpointName, e.AssetName);
                AssetChanged?.Invoke(this, new(e.DeviceName, e.InboundEndpointName, e.AssetName, ChangeType.Deleted, null));
            }
            else if (e.ChangeType == AssetFileMonitorChangeType.Created)
            {
                var notificationResponse = await _client.ObserveAssetUpdatesAsync(e.DeviceName, e.InboundEndpointName, e.AssetName);

                if (notificationResponse == NotificationResponse.Accepted)
                {
                    if (!_observedAssets.ContainsKey(e.DeviceName))
                    {
                        _observedAssets.TryAdd(e.DeviceName, new());
                    }

                    if (_observedAssets.TryGetValue(e.DeviceName, out var assets))
                    {
                        assets.Add(e.AssetName);
                    }

                    var asset = await _client.GetAssetAsync(e.DeviceName, e.InboundEndpointName, new GetAssetRequest() { AssetName = e.AssetName });
                    AssetChanged?.Invoke(this, new(e.DeviceName, e.InboundEndpointName, e.AssetName, ChangeType.Created, asset));
                }

                //TODO what if response is negative?
            }
        }

        private async void DeviceFileChanged(object? sender, Assets.DeviceChangedEventArgs e)
        {
            if (e.ChangeType == AssetFileMonitorChangeType.Deleted)
            {
                await _client.UnobserveDeviceEndpointUpdatesAsync(e.DeviceName, e.InboundEndpointName);
                DeviceChanged?.Invoke(this, new(e.DeviceName, e.InboundEndpointName, ChangeType.Deleted, null));
            }
            else if (e.ChangeType == AssetFileMonitorChangeType.Created)
            {
                var notificationResponse = await _client.ObserveDeviceEndpointUpdatesAsync(e.DeviceName, e.InboundEndpointName);

                if (notificationResponse == NotificationResponse.Accepted)
                {
                    _observedDevices.TryAdd(e.DeviceName, _dummyByte);
                    var device = await _client.GetDeviceAsync(e.DeviceName, e.InboundEndpointName);
                    DeviceChanged?.Invoke(this, new(e.DeviceName, e.InboundEndpointName, ChangeType.Created, device));
                }

                //TODO what if response is negative?
            }
        }
    }
}
