// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Moq;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    public class MockAdrClientWrapper : IAdrClientWrapper
    {
        public Mock<IAdrClientWrapper> mockClientWrapper { get; set; } = new Mock<IAdrClientWrapper>();

        public event EventHandler<AssetChangedEventArgs>? AssetChanged;
        public event EventHandler<DeviceChangedEventArgs>? DeviceChanged;

        public void SimulateAssetChanged(AssetChangedEventArgs args)
        {
            AssetChanged?.Invoke(this, args);
        }

        public void SimulateDeviceChanged(DeviceChangedEventArgs args)
        {
            DeviceChanged?.Invoke(this, args);
        }

        public IEnumerable<string> GetAssetNames(string deviceName, string inboundEndpointName)
        {
            return mockClientWrapper.Object.GetAssetNames(deviceName, inboundEndpointName);
        }

        public EndpointCredentials GetEndpointCredentials(string deviceName, string inboundEndpointName, InboundEndpointSchemaMapValue inboundEndpoint)
        {
            return mockClientWrapper.Object.GetEndpointCredentials(deviceName, inboundEndpointName, inboundEndpoint);
        }

        public IEnumerable<string> GetDeviceNames()
        {
            return mockClientWrapper.Object.GetDeviceNames();
        }

        public IEnumerable<string> GetInboundEndpointNames(string deviceName)
        {
            return mockClientWrapper.Object.GetInboundEndpointNames(deviceName);
        }

        public void ObserveAssets(string deviceName, string inboundEndpointName)
        {
            mockClientWrapper.Object.ObserveAssets(deviceName, inboundEndpointName);
        }

        public void ObserveDevices()
        {
            mockClientWrapper.Object.ObserveDevices();
        }

        public Task UnobserveAllAsync(CancellationToken cancellationToken = default)
        {
            return mockClientWrapper.Object.UnobserveAllAsync();
        }

        public Task UnobserveAssetsAsync(string deviceName, string inboundEndpointName, CancellationToken cancellationToken = default)
        {
            return mockClientWrapper.Object.UnobserveAssetsAsync(deviceName, inboundEndpointName, cancellationToken);
        }

        public Task UnobserveDevicesAsync(CancellationToken cancellationToken = default)
        {
            return mockClientWrapper.Object.UnobserveDevicesAsync(cancellationToken);
        }

        public Task<AssetStatus> UpdateAssetStatusAsync(string deviceName, string inboundEndpointName, UpdateAssetStatusRequest request, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return mockClientWrapper.Object.UpdateAssetStatusAsync(deviceName, inboundEndpointName, request, commandTimeout, cancellationToken);
        }

        public Task<DeviceStatus> UpdateDeviceStatusAsync(string deviceName, string inboundEndpointName, DeviceStatus status, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return mockClientWrapper.Object.UpdateDeviceStatusAsync(deviceName, inboundEndpointName, status, commandTimeout, cancellationToken);
        }

        public Task<CreateOrUpdateDiscoveredAssetResponsePayload> CreateOrUpdateDiscoveredAssetAsync(string deviceName, string inboundEndpointName, CreateOrUpdateDiscoveredAssetRequest request, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return mockClientWrapper.Object.CreateOrUpdateDiscoveredAssetAsync(deviceName, inboundEndpointName, request, commandTimeout, cancellationToken);
        }

        public Task<CreateOrUpdateDiscoveredDeviceResponsePayload> CreateOrUpdateDiscoveredDeviceAsync(CreateOrUpdateDiscoveredDeviceRequestSchema request, string inboundEndpointType, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return mockClientWrapper.Object.CreateOrUpdateDiscoveredDeviceAsync(request, inboundEndpointType, commandTimeout, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
