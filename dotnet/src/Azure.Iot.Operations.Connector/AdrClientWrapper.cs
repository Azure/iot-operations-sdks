// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public class AdrClientWrapper // TODO naming
    {
        private readonly IAdrServiceClient _client;
        private readonly IAssetFileMonitor _monitor;
        private readonly HashSet<string> _observedAssetEndpointProfiles = new();
        private readonly Dictionary<string, HashSet<string>> _observedAssets = new(); // TODO concurrency

        public event EventHandler<AssetChangedEventArgs>? AssetChanged;

        public event EventHandler<AssetEndpointProfileChangedEventArgs>? AssetEndpointProfileChanged;

        public AdrClientWrapper(ApplicationContext applicationContext, IMqttPubSubClient mqttPubSubClient)
        {
            _client = new AdrServiceClient(applicationContext, mqttPubSubClient);
            _client.OnReceiveAssetUpdateEventTelemetry += AssetUpdateReceived;
            _client.OnReceiveAssetEndpointProfileUpdateTelemetry += AssetEndpointProfileUpdateReceived;
            _monitor = new AssetFileMonitor();
            _monitor.AssetEndpointProfileCreated += AssetEndpointProfileFileCreated;
            _monitor.AssetEndpointProfileDeleted += AssetEndpointProfileFileDeleted;
            _monitor.AssetCreated += AssetFileCreated;
            _monitor.AssetDeleted += AssetFileDeleted;
        }

        public void Start()
        {
            _monitor.ObserveAssetEndpointProfile();
            _monitor.ObserveAssets();
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _monitor.UnobserveAssetEndpointProfile();
            _monitor.UnobserveAssets();

            foreach (string assetEndpointProfileName in _observedAssets.Keys)
            {
                foreach (string observedAssetName in _observedAssets[assetEndpointProfileName])
                {
                    await _client.UnobserveAssetUpdatesAsync(assetEndpointProfileName, observedAssetName, null, cancellationToken);
                }
            }

            _observedAssets.Clear();

            foreach (string assetEndpointProfileName in _observedAssetEndpointProfiles)
            {
                await _client.UnobserveAssetEndpointProfileUpdatesAsync(assetEndpointProfileName, null, cancellationToken);
            }

            _observedAssetEndpointProfiles.Clear();
        }

        private Task AssetEndpointProfileUpdateReceived(string arg1, AssetEndpointProfile? profile)
        {
            AssetEndpointProfileChanged?.Invoke(this, new(profile.Name, ChangeType.Updated, profile));
            return Task.CompletedTask;
        }

        private Task AssetUpdateReceived(string arg1, Asset? asset)
        {
            AssetChanged?.Invoke(this, new(e.AssetEndpointProfileName, asset.Name, ChangeType.Updated, asset));
            return Task.CompletedTask;
        }

        private void AssetFileDeleted(object? sender, AssetDeletedEventArgs e)
        {
            _client.UnobserveAssetUpdatesAsync(e.AssetEndpointProfileName, e.AssetName);
            AssetChanged?.Invoke(this, new(e.AssetEndpointProfileName, e.AssetName, ChangeType.Deleted, null));
        }

        private async void AssetFileCreated(object? sender, AssetCreatedEventArgs e)
        {
            var notificationResponse = await _client.ObserveAssetUpdatesAsync(e.AssetEndpointProfileName, e.AssetName);

            if (notificationResponse == NotificationResponse.Accepted)
            {
                if (_observedAssets[e.AssetEndpointProfileName] != null)
                {
                    _observedAssets[e.AssetEndpointProfileName] = new();
                }

                _observedAssets[e.AssetEndpointProfileName].Add(e.AssetName);

                var asset = await _client.GetAssetAsync(e.AssetEndpointProfileName, new GetAssetRequest() { AssetName = e.AssetName });
                AssetChanged?.Invoke(this, new(e.AssetEndpointProfileName, e.AssetName, ChangeType.Created, null));

            }

            //TODO what if response is negative?
        }

        private void AssetEndpointProfileFileDeleted(object? sender, AssetEndpointProfileDeletedEventArgs e)
        {
            _client.UnobserveAssetEndpointProfileUpdatesAsync(e.AssetEndpointProfileName);
            AssetEndpointProfileChanged?.Invoke(this, new(e.AssetEndpointProfileName, ChangeType.Deleted, null));
        }

        private async void AssetEndpointProfileFileCreated(object? sender, AssetEndpointProfileCreatedEventArgs e)
        {
            var notificationResponse = await _client.ObserveAssetEndpointProfileUpdatesAsync(e.AssetEndpointProfileName);

            if (notificationResponse == NotificationResponse.Accepted)
            {
                _observedAssetEndpointProfiles.Add(e.AssetEndpointProfileName);
                var assetEndpointProfile = await _client.GetAssetEndpointProfileAsync(e.AssetEndpointProfileName);
                AssetEndpointProfileChanged?.Invoke(this, new(e.AssetEndpointProfileName, ChangeType.Created, assetEndpointProfile));
            }

            //TODO what if response is negative?
        }
    }
}
