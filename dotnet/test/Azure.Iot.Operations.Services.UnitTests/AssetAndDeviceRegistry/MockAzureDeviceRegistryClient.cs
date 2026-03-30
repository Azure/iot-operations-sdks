// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Azure.Iot.Operations.Services.UnitTests.AssetAndDeviceRegistry
{
    public class MockAzureDeviceRegistryClient : IAzureDeviceRegistryClient // This implementation is currently only used to test reporting runtime health status, so most of the implementations are not actually implemented
    {
        public List<ReportedDeviceEndpointRuntimeHealth> ReportedDeviceEndpointRuntimeHealths = new();

        public class ReportedDeviceEndpointRuntimeHealth
        {
            public string DeviceName { get; set; }

            public string InboundEndpointName { get; set; }

            public RuntimeHealth RuntimeHealth { get; set; }

            public DateTime DateTimeReported { get; set; } = DateTime.UtcNow;

            public ReportedDeviceEndpointRuntimeHealth(string deviceName, string inboundEndpointName, RuntimeHealth health)
            {
                DeviceName = deviceName;
                InboundEndpointName = inboundEndpointName;
                RuntimeHealth = health;
            }
        }

        public List<ReportedDatasetRuntimeHealth> ReportedDatasetRuntimeHealths = new();

        public class ReportedDatasetRuntimeHealth
        {
            public string DeviceName { get; set; }

            public string InboundEndpointName { get; set; }

            public string AssetName { get; set; }

            public string DatasetName { get; set; }

            public RuntimeHealth RuntimeHealth { get; set; }

            public DateTime DateTimeReported { get; set; } = DateTime.UtcNow;

            public ReportedDatasetRuntimeHealth(string deviceName, string inboundEndpointName, string assetName, string datasetName, RuntimeHealth health)
            {
                DeviceName = deviceName;
                InboundEndpointName = inboundEndpointName;
                AssetName = assetName;
                DatasetName = datasetName;
                RuntimeHealth = health;
            }
        }

        public Task ReportDeviceEndpointRuntimeHealthAsync(string deviceName, string inboundEndpointName, RuntimeHealth deviceEndpointRuntimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            ReportedDeviceEndpointRuntimeHealths.Add(new ReportedDeviceEndpointRuntimeHealth(deviceName, inboundEndpointName, deviceEndpointRuntimeHealth));
            return Task.CompletedTask;
        }

        public Task ReportDatasetRuntimeHealthAsync(string deviceName, string inboundEndpointName, string assetName, List<DatasetsRuntimeHealthEvent> datasetsRuntimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            foreach (DatasetsRuntimeHealthEvent datasetHealthEvent in datasetsRuntimeHealth)
            {
                ReportedDatasetRuntimeHealths.Add(new ReportedDatasetRuntimeHealth(deviceName, inboundEndpointName, assetName, datasetHealthEvent.DatasetName, datasetHealthEvent.RuntimeHealth));
            }

            return Task.CompletedTask;
        }

        public Task ReportEventRuntimeHealthAsync(string deviceName, string inboundEndpointName, string assetName, List<EventsRuntimeHealthEvent> eventsRuntimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ReportStreamRuntimeHealthAsync(string deviceName, string inboundEndpointName, string assetName, List<StreamsRuntimeHealthEvent> streamsRuntimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ReportManagementActionRuntimeHealthAsync(string deviceName, string inboundEndpointName, string assetName, List<ManagementActionsRuntimeHealthEvent> managementActionsRuntimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

#pragma warning disable CS0067 // Unused, but required to implement this interface
        public event Func<string, string, Device, Task>? OnReceiveDeviceUpdateEventTelemetry;
        public event Func<string, Asset, Task>? OnReceiveAssetUpdateEventTelemetry;
#pragma warning restore CS0067 // Unused, but required to implement this interface

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
            throw new NotImplementedException();
        }

        public Task<AssetStatus> GetAssetStatusAsync(string deviceName, string inboundEndpointName, string assetName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Device> GetDeviceAsync(string deviceName, string inboundEndpointName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DeviceStatus> GetDeviceStatusAsync(string deviceName, string inboundEndpointName, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<SetNotificationPreferenceForAssetUpdatesResponsePayload> SetNotificationPreferenceForAssetUpdatesAsync(string deviceName, string inboundEndpointName, string assetName, NotificationPreference notificationPreference, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<SetNotificationPreferenceForDeviceUpdatesResponsePayload> SetNotificationPreferenceForDeviceUpdatesAsync(string deviceName, string inboundEndpointName, NotificationPreference notificationPreference, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<AssetStatus> UpdateAssetStatusAsync(string deviceName, string inboundEndpointName, UpdateAssetStatusRequest request, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DeviceStatus> UpdateDeviceStatusAsync(string deviceName, string inboundEndpointName, DeviceStatus status, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
