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

        public Dictionary<string, List<ReportedDatasetRuntimeHealth>> ReportedDatasetRuntimeHealths = new();

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

        public Dictionary<string, List<ReportedStreamRuntimeHealth>> ReportedStreamRuntimeHealths = new();

        public class ReportedStreamRuntimeHealth
        {
            public string DeviceName { get; set; }

            public string InboundEndpointName { get; set; }

            public string AssetName { get; set; }

            public string StreamName { get; set; }

            public RuntimeHealth RuntimeHealth { get; set; }

            public DateTime DateTimeReported { get; set; } = DateTime.UtcNow;

            public ReportedStreamRuntimeHealth(string deviceName, string inboundEndpointName, string assetName, string streamName, RuntimeHealth health)
            {
                DeviceName = deviceName;
                InboundEndpointName = inboundEndpointName;
                AssetName = assetName;
                StreamName = streamName;
                RuntimeHealth = health;
            }
        }

        public Dictionary<string, Dictionary<string, List<ReportedEventRuntimeHealth>>> ReportedEventRuntimeHealths = new();

        public class ReportedEventRuntimeHealth
        {
            public string DeviceName { get; set; }

            public string InboundEndpointName { get; set; }

            public string AssetName { get; set; }

            public string EventGroupName { get; set; }

            public string EventName { get; set; }

            public RuntimeHealth RuntimeHealth { get; set; }

            public DateTime DateTimeReported { get; set; } = DateTime.UtcNow;

            public ReportedEventRuntimeHealth(string deviceName, string inboundEndpointName, string assetName, string eventGroupName, string eventName, RuntimeHealth health)
            {
                DeviceName = deviceName;
                InboundEndpointName = inboundEndpointName;
                AssetName = assetName;
                EventGroupName = eventGroupName;
                EventName = eventName;
                RuntimeHealth = health;
            }
        }

        public Dictionary<string, Dictionary<string, List<ReportedManagementActionRuntimeHealth>>> ReportedManagementActionRuntimeHealths = new();

        public class ReportedManagementActionRuntimeHealth
        {
            public string DeviceName { get; set; }

            public string InboundEndpointName { get; set; }

            public string AssetName { get; set; }

            public string ManagementGroupName { get; set; }

            public string ManagementActionName { get; set; }

            public RuntimeHealth RuntimeHealth { get; set; }

            public DateTime DateTimeReported { get; set; } = DateTime.UtcNow;

            public ReportedManagementActionRuntimeHealth(string deviceName, string inboundEndpointName, string assetName, string managementGroupName, string managementActionName, RuntimeHealth health)
            {
                DeviceName = deviceName;
                InboundEndpointName = inboundEndpointName;
                AssetName = assetName;
                ManagementGroupName = managementGroupName;
                ManagementActionName = managementActionName;
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
                if (!ReportedDatasetRuntimeHealths.ContainsKey(datasetHealthEvent.DatasetName))
                {
                    ReportedDatasetRuntimeHealths.Add(datasetHealthEvent.DatasetName, new List<ReportedDatasetRuntimeHealth>());
                }

                ReportedDatasetRuntimeHealths[datasetHealthEvent.DatasetName].Add(new ReportedDatasetRuntimeHealth(deviceName, inboundEndpointName, assetName, datasetHealthEvent.DatasetName, datasetHealthEvent.RuntimeHealth));
            }

            return Task.CompletedTask;
        }

        public Task ReportEventRuntimeHealthAsync(string deviceName, string inboundEndpointName, string assetName, List<EventsRuntimeHealthEvent> eventsRuntimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            foreach (EventsRuntimeHealthEvent eventGroupHealthEvents in eventsRuntimeHealth)
            {
                if (!ReportedEventRuntimeHealths.ContainsKey(eventGroupHealthEvents.EventGroupName))
                {
                    ReportedEventRuntimeHealths.Add(eventGroupHealthEvents.EventGroupName, new Dictionary<string, List<ReportedEventRuntimeHealth>>());
                }

                if (!ReportedEventRuntimeHealths[eventGroupHealthEvents.EventGroupName].ContainsKey(eventGroupHealthEvents.EventName))
                {
                    ReportedEventRuntimeHealths[eventGroupHealthEvents.EventGroupName][eventGroupHealthEvents.EventName] = new List<ReportedEventRuntimeHealth>();
                }

                ReportedEventRuntimeHealths[eventGroupHealthEvents.EventGroupName][eventGroupHealthEvents.EventName].Add(new ReportedEventRuntimeHealth(deviceName, inboundEndpointName, assetName, eventGroupHealthEvents.EventGroupName, eventGroupHealthEvents.EventName, eventGroupHealthEvents.RuntimeHealth));
            }

            return Task.CompletedTask;
        }

        public Task ReportStreamRuntimeHealthAsync(string deviceName, string inboundEndpointName, string assetName, List<StreamsRuntimeHealthEvent> streamsRuntimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            foreach (StreamsRuntimeHealthEvent streamHealthEvent in streamsRuntimeHealth)
            {
                if (!ReportedStreamRuntimeHealths.ContainsKey(streamHealthEvent.StreamName))
                {
                    ReportedStreamRuntimeHealths.Add(streamHealthEvent.StreamName, new List<ReportedStreamRuntimeHealth>());
                }

                ReportedStreamRuntimeHealths[streamHealthEvent.StreamName].Add(new ReportedStreamRuntimeHealth(deviceName, inboundEndpointName, assetName, streamHealthEvent.StreamName, streamHealthEvent.RuntimeHealth));
            }

            return Task.CompletedTask;
        }

        public Task ReportManagementActionRuntimeHealthAsync(string deviceName, string inboundEndpointName, string assetName, List<ManagementActionsRuntimeHealthEvent> managementActionsRuntimeHealth, TimeSpan? telemetryTimeout = null, CancellationToken cancellationToken = default)
        {
            foreach (ManagementActionsRuntimeHealthEvent managementGroupHealthEvents in managementActionsRuntimeHealth)
            {
                if (!ReportedManagementActionRuntimeHealths.ContainsKey(managementGroupHealthEvents.ManagementGroupName))
                {
                    ReportedManagementActionRuntimeHealths.Add(managementGroupHealthEvents.ManagementGroupName, new Dictionary<string, List<ReportedManagementActionRuntimeHealth>>());
                }

                if (!ReportedManagementActionRuntimeHealths[managementGroupHealthEvents.ManagementGroupName].ContainsKey(managementGroupHealthEvents.ManagementActionName))
                {
                    ReportedManagementActionRuntimeHealths[managementGroupHealthEvents.ManagementGroupName][managementGroupHealthEvents.ManagementActionName] = new List<ReportedManagementActionRuntimeHealth>();
                }

                ReportedManagementActionRuntimeHealths[managementGroupHealthEvents.ManagementGroupName][managementGroupHealthEvents.ManagementActionName].Add(new ReportedManagementActionRuntimeHealth(deviceName, inboundEndpointName, assetName, managementGroupHealthEvents.ManagementGroupName, managementGroupHealthEvents.ManagementActionName, managementGroupHealthEvents.RuntimeHealth));
            }

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
