﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public class AdrClientWrapper // TODO naming
    {
        //TODO nullablility of required fields in DTDL model types

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
            _monitor.DeviceCreated += AssetEndpointProfileFileCreated;
            _monitor.DeviceDeleted += AssetEndpointProfileFileDeleted;
            _monitor.AssetCreated += AssetFileCreated;
            _monitor.AssetDeleted += AssetFileDeleted;
        }

        public void ObserveAssetEndpointProfiles()
        {
            _monitor.ObserveDevices();
        }

        public void ObserveAssets(string aepName)
        {
            _monitor.ObserveAssets(aepName);
        }

        public void UnobserveAssetEndpointProfiles()
        {
            _monitor.UnobserveDevices();
            //_client.UnobserveAssetEndpointProfileUpdatesAsync(); //TODO all at once or one AEP name at a time?
        }

        public void UnobserveAssets(string aepName)
        {
            _monitor.UnobserveAssets(aepName);
        }

        public async Task UnobserveAllAsync(CancellationToken cancellationToken = default)
        {
            _monitor.UnobserveAll();

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
            AssetEndpointProfileChanged?.Invoke(this, new(profile!.Name!, ChangeType.Updated, profile));
            return Task.CompletedTask;
        }

        private Task AssetUpdateReceived(string arg1, Asset? asset)
        {
            //TODO bit of leap on this assumption
            AssetChanged?.Invoke(this, new(asset!.Specification!.AssetEndpointProfileRef!.Split("/")[1], asset!.Name!, ChangeType.Updated, asset!));
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

        private void AssetEndpointProfileFileDeleted(object? sender, DeviceDeletedEventArgs e)
        {
            _client.UnobserveAssetEndpointProfileUpdatesAsync(e.AssetEndpointProfileName);
            AssetEndpointProfileChanged?.Invoke(this, new(e.AssetEndpointProfileName, ChangeType.Deleted, null));
        }

        private async void AssetEndpointProfileFileCreated(object? sender, DeviceCreatedEventArgs e)
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
