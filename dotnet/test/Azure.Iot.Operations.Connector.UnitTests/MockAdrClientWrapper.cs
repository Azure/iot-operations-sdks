// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    public class MockAdrClientWrapper : IAdrClientWrapper
    {
#pragma warning disable CS0067 // only unused while this mock is being filled out
        public event EventHandler<AssetChangedEventArgs>? AssetChanged;
        public event EventHandler<DeviceChangedEventArgs>? DeviceChanged;
#pragma warning restore CS0067

        public IEnumerable<string> GetAssetNames(string deviceName, string inboundEndpointName)
        {
            throw new NotImplementedException();
        }

        public Assets.DeviceCredentials GetDeviceCredentials(string deviceName, string inboundEndpointName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetDeviceNames()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetInboundEndpointNames(string deviceName)
        {
            throw new NotImplementedException();
        }

        public void ObserveAssets(string deviceName, string inboundEndpointName)
        {
            throw new NotImplementedException();
        }

        public void ObserveDevices()
        {
            throw new NotImplementedException();
        }

        public Task UnobserveAllAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task UnobserveAssetsAsync(string deviceName, string inboundEndpointName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task UnobserveDevicesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Asset> UpdateAssetStatusAsync(string deviceName, string inboundEndpointName, UpdateAssetStatusRequest request, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Device> UpdateDeviceStatusAsync(string deviceName, string inboundEndpointName, DeviceStatus status, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
