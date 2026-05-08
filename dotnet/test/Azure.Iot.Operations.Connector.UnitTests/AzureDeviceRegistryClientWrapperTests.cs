// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    public class AzureDeviceRegistryClientWrapperTests
    {
        [Fact]
        public async Task UnobserveAllUsesCorrectDeviceAndInboundEndpointNames()
        {
            string deviceName = "someDeviceName";
            string inboundEndpointName = "someInboundEndpointName";
            var mockAdrServiceClient = new MockAdrServiceClient();
            var mockAssetFileMonitor = new MockAssetFileMonitor();
            AzureDeviceRegistryClientWrapper adrClientWrapper = new(mockAdrServiceClient, mockAssetFileMonitor);
            adrClientWrapper.ObserveDevices();

            TaskCompletionSource onDeviceChangedTcs = new();
            adrClientWrapper.DeviceChanged += (sender, args) =>
            {
                onDeviceChangedTcs.TrySetResult();
            };

            mockAssetFileMonitor.SimulateNewDeviceCreated(deviceName, inboundEndpointName);

            try
            {
                await onDeviceChangedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for the adr client wrapper to report that a device changed");
            }

            // Now that the ADR client wrapper has observed a device, try unobserving all to check that it unobserves the expected single device
            await adrClientWrapper.UnobserveAllAsync();
            Assert.Equal(2, mockAdrServiceClient.DeviceNotificationChangesSent.Count);

            // The first notification change should be to subscribe to notifications for the single device
            Assert.Equal(deviceName, mockAdrServiceClient.DeviceNotificationChangesSent[0].DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrServiceClient.DeviceNotificationChangesSent[0].InboundEndpointName);
            Assert.True(mockAdrServiceClient.DeviceNotificationChangesSent[0].IsSubscribe);

            // The second notification change should be to unsubscribe from notifications for the single device
            Assert.Equal(deviceName, mockAdrServiceClient.DeviceNotificationChangesSent[1].DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrServiceClient.DeviceNotificationChangesSent[1].InboundEndpointName);
            Assert.False(mockAdrServiceClient.DeviceNotificationChangesSent[1].IsSubscribe);
        }

        [Fact]
        public async Task OnDeviceFileChangeBehavesIdempotently()
        {
            string deviceName = "someDeviceName";
            string inboundEndpointName = "someInboundEndpointName";
            var mockAdrServiceClient = new MockAdrServiceClient();
            var mockAssetFileMonitor = new MockAssetFileMonitor();
            AzureDeviceRegistryClientWrapper adrClientWrapper = new(mockAdrServiceClient, mockAssetFileMonitor);
            adrClientWrapper.ObserveDevices();

            TaskCompletionSource onDeviceChangedTcs = new();
            adrClientWrapper.DeviceChanged += (sender, args) =>
            {
                onDeviceChangedTcs.TrySetResult();
            };

            mockAssetFileMonitor.SimulateNewDeviceCreated(deviceName, inboundEndpointName);

            try
            {
                await onDeviceChangedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for the adr client wrapper to report that a device changed");
            }

            Assert.Equal(1, mockAdrServiceClient.NotificationPreferencesSetForDevice);

            onDeviceChangedTcs = new();
            mockAssetFileMonitor.SimulateNewDeviceCreated(deviceName, inboundEndpointName);

            try
            {
                await onDeviceChangedTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch (TimeoutException)
            {
                // expected to time out since the wrapper should not re-report this device as created
            }

            Assert.Equal(1, mockAdrServiceClient.NotificationPreferencesSetForDevice);

            onDeviceChangedTcs = new();
            mockAssetFileMonitor.SimulateNewDeviceDeleted(deviceName, inboundEndpointName);

            try
            {
                await onDeviceChangedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for the adr client wrapper to report that a device changed");
            }

            // No new notification preferences should be set since the wrapper doesn't do that when a device is deleted
            Assert.Equal(1, mockAdrServiceClient.NotificationPreferencesSetForDevice);

            onDeviceChangedTcs = new();
            mockAssetFileMonitor.SimulateNewDeviceDeleted(deviceName, inboundEndpointName);

            try
            {
                await onDeviceChangedTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch (TimeoutException)
            {
                // expected to time out since the wrapper should not re-report this device as deleted
            }

            Assert.Equal(1, mockAdrServiceClient.NotificationPreferencesSetForDevice);
        }

        [Fact]
        public async Task OnAssetFileChangeBehavesIdempotently()
        {
            string deviceName = "someDeviceName";
            string inboundEndpointName = "someInboundEndpointName";
            string assetName = "someAsset";
            var mockAdrServiceClient = new MockAdrServiceClient();
            var mockAssetFileMonitor = new MockAssetFileMonitor();
            AzureDeviceRegistryClientWrapper adrClientWrapper = new(mockAdrServiceClient, mockAssetFileMonitor);
            adrClientWrapper.ObserveDevices();

            TaskCompletionSource onAssetChangedTcs = new();
            adrClientWrapper.AssetChanged += (sender, args) =>
            {
                onAssetChangedTcs.TrySetResult();
            };

            mockAssetFileMonitor.SimulateNewDeviceCreated(deviceName, inboundEndpointName);
            adrClientWrapper.ObserveAssets(deviceName, inboundEndpointName);
            mockAssetFileMonitor.SimulateNewAssetCreated(deviceName, inboundEndpointName, assetName);

            try
            {
                await onAssetChangedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for the adr client wrapper to report that an asset changed");
            }

            Assert.Equal(1, mockAdrServiceClient.NotificationPreferencesSetForAsset);

            onAssetChangedTcs = new();
            mockAssetFileMonitor.SimulateNewAssetCreated(deviceName, inboundEndpointName, assetName);

            try
            {
                await onAssetChangedTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch (TimeoutException)
            {
                // expected to time out since the wrapper should not re-report this asset as created
            }

            Assert.Equal(1, mockAdrServiceClient.NotificationPreferencesSetForAsset);

            onAssetChangedTcs = new();
            mockAssetFileMonitor.SimulateNewAssetDeleted(deviceName, inboundEndpointName, assetName);

            try
            {
                await onAssetChangedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for the adr client wrapper to report that an asset changed");
            }

            // No new notification preferences should be set since the wrapper doesn't do that when an asset is deleted
            Assert.Equal(1, mockAdrServiceClient.NotificationPreferencesSetForAsset);

            onAssetChangedTcs = new();
            mockAssetFileMonitor.SimulateNewAssetDeleted(deviceName, inboundEndpointName, assetName);

            try
            {
                await onAssetChangedTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch (TimeoutException)
            {
                // expected to time out since the wrapper should not re-report this asset as deleted
            }

            Assert.Equal(1, mockAdrServiceClient.NotificationPreferencesSetForAsset);
        }
    }
}
