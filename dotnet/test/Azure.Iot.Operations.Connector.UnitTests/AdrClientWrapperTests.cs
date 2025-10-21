// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    public class AdrClientWrapperTests
    {
        [Fact]
        public async Task UnobserveAllUsesCorrectDeviceAndInboundEndpointNames()
        {
            string deviceName = "someDeviceName";
            string inboundEndpointName = "someInboundEndpointName";
            var mockAdrServiceClient = new MockAdrServiceClient();
            var mockAssetFileMonitor = new MockAssetFileMonitor();
            AdrClientWrapper adrClientWrapper = new(mockAdrServiceClient, mockAssetFileMonitor);
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
    }
}
