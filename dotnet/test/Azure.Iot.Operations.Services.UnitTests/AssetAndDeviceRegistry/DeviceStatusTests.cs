// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.AssetAndDeviceRegistry
{
    public class DeviceStatusTests
    {
        [Fact]
        public void TestComparison()
        {
            DeviceStatus status1 = createTestDeviceStatus();
            DeviceStatus status2 = createTestDeviceStatus();

            Assert.True(status1.EqualTo(status2));

            status1.Config!.LastTransitionTime = DateTime.UtcNow;

            // 'LastTransitionTime' field differences should be ignored
            Assert.True(status1.EqualTo(status2));

            status1.Config!.Error = new ConfigError()
            {
                Code = "someErrorCode",
            };

            Assert.False(status1.EqualTo(status2));

            DeviceStatus status3 = createTestDeviceStatus();
            status3.Config!.Version = 10;
            Assert.False(status2.EqualTo(status3));

            DeviceStatus status4 = createTestDeviceStatus();
            status4.Endpoints = new();
            Assert.False(status2.EqualTo(status4));

            DeviceStatus status5 = createTestDeviceStatus();
            status5.Endpoints!.Inbound!.Add("someNewKey", new());
            Assert.False(status2.EqualTo(status5));
        }

        private DeviceStatus createTestDeviceStatus()
        {
            return new DeviceStatus()
            {
                Config = new ConfigStatus()
                {
                    Error = null,
                    LastTransitionTime = DateTime.MinValue,
                    Version = 1,
                },
                Endpoints = new DeviceStatusEndpoint()
                {
                    Inbound = new Dictionary<string, DeviceStatusInboundEndpointSchemaMapValue>
                    {
                        { "someInboundEndpoint", new DeviceStatusInboundEndpointSchemaMapValue() { Error = null } }
                    }
                }
            };
        }
    }
}
