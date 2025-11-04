// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    // Note that this is mostly unimplemented. It was only added for mocking around setting notification preference.
    public class MockAdrServiceClient : IAdrServiceClient
    {
        public event Func<string, string, Device, Task>? OnReceiveDeviceUpdateEventTelemetry;
        public event Func<string, Asset, Task>? OnReceiveAssetUpdateEventTelemetry;


        public List<SetNotificationPreferenceRecord> DeviceNotificationChangesSent = new();

        public void SimulateDeviceUpdate(string deviceName, string inboundEndpointName, Device device)
        {
            OnReceiveDeviceUpdateEventTelemetry?.Invoke(deviceName, inboundEndpointName, device);
        }

        public void SimulateAssetUpdate(string assetName, Asset asset)
        {
            OnReceiveAssetUpdateEventTelemetry?.Invoke(assetName, asset);
        }

        public Task<CreateOrUpdateDiscoveredAssetResponsePayload> CreateOrUpdateDiscoveredAssetAsync(string deviceName, string inboundEndpointName, CreateOrUpdateDiscoveredAssetRequest request, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<CreateOrUpdateDiscoveredDeviceResponsePayload> CreateOrUpdateDiscoveredDeviceAsync(CreateOrUpdateDiscoveredDeviceRequestSchema request, string inboundEndpointType, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public Task<Asset> GetAssetAsync(string deviceName, string inboundEndpointName, string assetName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Asset()
            {
                DisplayName = assetName,
                DeviceRef = new()
                {
                    DeviceName = deviceName,
                    EndpointName = inboundEndpointName
                }
            });
        }

        public Task<AssetStatus> GetAssetStatusAsync(string deviceName, string inboundEndpointName, string assetName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AssetStatus());
        }

        public Task<Device> GetDeviceAsync(string deviceName, string inboundEndpointName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Device());
        }

        public Task<DeviceStatus> GetDeviceStatusAsync(string deviceName, string inboundEndpointName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DeviceStatus());
        }

        public Task<SetNotificationPreferenceForAssetUpdatesResponsePayload> SetNotificationPreferenceForAssetUpdatesAsync(string deviceName, string inboundEndpointName, string assetName, NotificationPreference notificationPreference, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SetNotificationPreferenceForAssetUpdatesResponsePayload()
            {
                ResponsePayload = "Accepted"
            });
        }

        public Task<SetNotificationPreferenceForDeviceUpdatesResponsePayload> SetNotificationPreferenceForDeviceUpdatesAsync(string deviceName, string inboundEndpointName, NotificationPreference notificationPreference, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            DeviceNotificationChangesSent.Add(new(deviceName, inboundEndpointName, notificationPreference == NotificationPreference.On));
            return Task.FromResult(new SetNotificationPreferenceForDeviceUpdatesResponsePayload()
            {
                ResponsePayload = "Accepted"
            });
        }

        public Task<AssetStatus> UpdateAssetStatusAsync(string deviceName, string inboundEndpointName, UpdateAssetStatusRequest request, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AssetStatus());
        }

        public Task<DeviceStatus> UpdateDeviceStatusAsync(string deviceName, string inboundEndpointName, DeviceStatus status, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DeviceStatus());
        }
    }

    public class SetNotificationPreferenceRecord
    {
        public string DeviceName { get; set; }

        public string InboundEndpointName { get; set; }

        public bool IsSubscribe { get; set; }

        public SetNotificationPreferenceRecord(string deviceName, string inboundEndpointName, bool isSubscribe)
        {
            DeviceName = deviceName;
            InboundEndpointName = inboundEndpointName;
            IsSubscribe = isSubscribe;
        }
    }
}
