// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Connector.Assets;
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

        public DeviceCredentials GetDeviceCredentials(string deviceName, string inboundEndpointName)
        {
            return mockClientWrapper.Object.GetDeviceCredentials(deviceName, inboundEndpointName);
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

        public Task<Asset> UpdateAssetStatusAsync(string deviceName, string inboundEndpointName, UpdateAssetStatusRequest request, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return mockClientWrapper.Object.UpdateAssetStatusAsync(deviceName, inboundEndpointName, request, commandTimeout, cancellationToken);
        }

        public Task<Device> UpdateDeviceStatusAsync(string deviceName, string inboundEndpointName, DeviceStatus status, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            return mockClientWrapper.Object.UpdateDeviceStatusAsync(deviceName, inboundEndpointName, status, commandTimeout, cancellationToken);
        }
    }
}
