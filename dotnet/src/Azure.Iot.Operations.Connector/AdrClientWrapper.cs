// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Azure.Iot.Operations.Connector.Assets;

namespace Azure.Iot.Operations.Connector
{
    public class AdrClientWrapper : IAdrClientWrapper // TODO naming
    {
        private readonly IAdrServiceClient _client;
        private readonly IAssetFileMonitor _monitor;
        private readonly HashSet<string> _observedDevices = new();
        private readonly Dictionary<string, HashSet<string>> _observedAssets = new(); // TODO concurrency

        public event EventHandler<AssetChangedEventArgs>? AssetChanged;

        public event EventHandler<DeviceChangedEventArgs>? DeviceChanged;

        public AdrClientWrapper(ApplicationContext applicationContext, IMqttPubSubClient mqttPubSubClient, string connectorClientId)
        {
            _client = new AdrServiceClient(applicationContext, mqttPubSubClient, connectorClientId);
            _client.OnReceiveAssetUpdateEventTelemetry += AssetUpdateReceived;
            _client.OnReceiveDeviceUpdateEventTelemetry += DeviceUpdateReceived;
            _monitor = new AssetFileMonitor();
            _monitor.DeviceFileCreated += DeviceCreated;
            _monitor.DeviceFileDeleted += DeviceDeleted;
            _monitor.AssetFileCreated += AssetCreated;
            _monitor.AssetFileDeleted += AssetDeleted;
        }

        public void ObserveDevices()
        {
            _monitor.ObserveDevices();
        }

        public void ObserveAssets(string deviceName, string inboundEndpointName)
        {
            _monitor.ObserveAssets(deviceName, inboundEndpointName);
        }

        public void UnobserveDevices()
        {
            _monitor.UnobserveDevices();
            //_client.UnobserveAssetEndpointProfileUpdatesAsync();
        }

        public void UnobserveAssets(string deviceName, string inboundEndpointName)
        {
            _monitor.UnobserveAssets(deviceName, inboundEndpointName);
        }

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

            foreach (string compositeDeviceName in _observedDevices)
            {
                string deviceName = compositeDeviceName.Split('_')[0];
                string inboundEndpointName = compositeDeviceName.Split('_')[1];
                await _client.UnobserveDeviceEndpointUpdatesAsync(deviceName, inboundEndpointName, null, cancellationToken);
            }

            _observedDevices.Clear();
        }

        public DeviceCredentials GetDeviceCredentials(string deviceName, string inboundEndpointName)
        {
            return _monitor.GetDeviceCredentials(deviceName, inboundEndpointName);
        }

        private Task DeviceUpdateReceived(string arg1, Device? device)
        {
            string deviceName = device.Name.Split('_')[0];
            string inboundEndpointName = device.Name.Split('_')[1];
            DeviceChanged?.Invoke(this, new(deviceName, inboundEndpointName, ChangeType.Updated, device));
            return Task.CompletedTask;
        }

        private Task AssetUpdateReceived(string arg1, Asset asset)
        {
            //TODO bit of leap on this assumption
            AssetChanged?.Invoke(this, new(asset.Specification.DeviceRef.DeviceName, asset!.Specification.DeviceRef.EndpointName, asset.Name!, ChangeType.Updated, asset!));
            return Task.CompletedTask;
        }

        private void AssetDeleted(object? sender, AssetDeletedEventArgs e)
        {
            _client.UnobserveAssetUpdatesAsync(e.DeviceName, e.InboundEndpointName, e.AssetName);
            AssetChanged?.Invoke(this, new(e.DeviceName, e.InboundEndpointName, e.AssetName, ChangeType.Deleted, null));
        }

        private async void AssetCreated(object? sender, AssetCreatedEventArgs e)
        {
            var notificationResponse = await _client.ObserveAssetUpdatesAsync(e.DeviceName, e.InboundEndpointName, e.AssetName);

            if (notificationResponse == NotificationResponse.Accepted)
            {
                if (_observedAssets[e.DeviceName] != null)
                {
                    _observedAssets[e.DeviceName] = new();
                }

                _observedAssets[e.DeviceName].Add(e.AssetName);

                var asset = await _client.GetAssetAsync(e.DeviceName, e.InboundEndpointName, new GetAssetRequest() { AssetName = e.AssetName });
                AssetChanged?.Invoke(this, new(e.DeviceName, e.InboundEndpointName, e.AssetName, ChangeType.Created, null));

            }

            //TODO what if response is negative?
        }

        private async void DeviceDeleted(object? sender, DeviceDeletedEventArgs e)
        {
            await _client.UnobserveDeviceEndpointUpdatesAsync(e.DeviceName, e.InboundEndpointName);
            DeviceChanged?.Invoke(this, new(e.DeviceName, e.InboundEndpointName, ChangeType.Deleted, null));
        }

        private async void DeviceCreated(object? sender, DeviceCreatedEventArgs e)
        {
            var notificationResponse = await _client.ObserveDeviceEndpointUpdatesAsync(e.DeviceName, e.InboundEndpointName);

            if (notificationResponse == NotificationResponse.Accepted)
            {
                _observedDevices.Add(e.DeviceName);
                var device = await _client.GetDeviceAsync(e.DeviceName, e.InboundEndpointName);
                DeviceChanged?.Invoke(this, new(e.DeviceName, e.InboundEndpointName, ChangeType.Created, device));
            }

            //TODO what if response is negative?
        }

        //TODO send update APIs
    }
}
