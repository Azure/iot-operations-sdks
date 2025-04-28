// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Connector.Assets;
using Xunit;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    // These tests rely on environment variables which may intefere with other similar tests
    [Collection("Environment Variable Sequential")]
    public class AssetFileMonitorTests
    {
        private const string AdrResourcesPath = "./adr-resources";

        [Fact]
        public void TestListingDevicesEndpointsAndAssets()
        {
            Environment.SetEnvironmentVariable(AssetFileMonitor.AdrResourcesNameMountPathEnvVar, AdrResourcesPath);

            AssetFileMonitor assetFileMonitor = new AssetFileMonitor();

            var deviceNames = assetFileMonitor.GetDeviceNames();
            Assert.Single(deviceNames);
            Assert.Equal("SomeDeviceName", deviceNames.First());

            var endpointNames = assetFileMonitor.GetInboundEndpointNames(deviceNames.First());
            Assert.Single(endpointNames);
            Assert.Equal("SomeInboundEndpointName", endpointNames.First());

            var assetNames = assetFileMonitor.GetAssetNames(deviceNames.First(), endpointNames.First());

            Assert.Equal(2, assetNames.Count());
            Assert.Contains("SomeAssetName1", assetNames);
            Assert.Contains("SomeAssetName2", assetNames);

            assetFileMonitor.UnobserveAll();
        }

        [Fact]
        public async Task TestObservingDevicesEndpointsAndAssets()
        {
            string expectedDeviceName = Guid.NewGuid().ToString();
            string expectedEndpointName = Guid.NewGuid().ToString();
            string devicePath = AdrResourcesPath + "/" + expectedDeviceName + "_" + expectedEndpointName;

            Environment.SetEnvironmentVariable(AssetFileMonitor.AdrResourcesNameMountPathEnvVar, AdrResourcesPath);

            AssetFileMonitor assetFileMonitor = new AssetFileMonitor();

            try
            {
                TaskCompletionSource<Assets.DeviceChangedEventArgs> deviceChangedEventArgsTcs = new();
                assetFileMonitor.DeviceFileChanged += (sender, args) =>
                {
                    deviceChangedEventArgsTcs.TrySetResult(args);
                };

                assetFileMonitor.ObserveDevices();

                // The initial state of the devices should be reported even before any changes happen
                var deviceChangeArgs = await deviceChangedEventArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal("SomeDeviceName", deviceChangeArgs.DeviceName);
                Assert.Equal("SomeInboundEndpointName", deviceChangeArgs.InboundEndpointName);
                Assert.Equal(AssetFileMonitorChangeType.Created, deviceChangeArgs.ChangeType);

                TaskCompletionSource<Assets.AssetChangedEventArgs> asset1ChangedEventArgsTcs = new();
                TaskCompletionSource<Assets.AssetChangedEventArgs> asset2ChangedEventArgsTcs = new();
                TaskCompletionSource<Assets.AssetChangedEventArgs> assetChangedEventArgsTcs = new();
                assetFileMonitor.AssetFileChanged += (sender, args) =>
                {
                    if (args.AssetName.Equals("SomeAssetName1"))
                    {
                        asset1ChangedEventArgsTcs.TrySetResult(args);
                    }
                    else if (args.AssetName.Equals("SomeAssetName2"))
                    {
                        asset2ChangedEventArgsTcs.TrySetResult(args);
                    }
                    else
                    {
                        assetChangedEventArgsTcs.TrySetResult(args);
                    }
                };

                assetFileMonitor.ObserveAssets(deviceChangeArgs.DeviceName, deviceChangeArgs.InboundEndpointName);

                // The initial state of the assets should be reported even before any changes happen
                var asset1ChangeArgs = await asset1ChangedEventArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal("SomeDeviceName", asset1ChangeArgs.DeviceName);
                Assert.Equal("SomeInboundEndpointName", asset1ChangeArgs.InboundEndpointName);
                Assert.Equal("SomeAssetName1", asset1ChangeArgs.AssetName);
                Assert.Equal(AssetFileMonitorChangeType.Created, asset1ChangeArgs.ChangeType);

                var asset2ChangeArgs = await asset2ChangedEventArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal("SomeDeviceName", asset2ChangeArgs.DeviceName);
                Assert.Equal("SomeInboundEndpointName", asset2ChangeArgs.InboundEndpointName);
                Assert.Equal("SomeAssetName2", asset2ChangeArgs.AssetName);
                Assert.Equal(AssetFileMonitorChangeType.Created, asset2ChangeArgs.ChangeType);

                // Add a new device + endpoint
                deviceChangedEventArgsTcs = new();
                File.Create(devicePath).Close();

                var deviceCreatedArgs = await deviceChangedEventArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Equal(expectedDeviceName, deviceCreatedArgs.DeviceName);
                Assert.Equal(expectedEndpointName, deviceCreatedArgs.InboundEndpointName);
                Assert.Equal(AssetFileMonitorChangeType.Created, deviceCreatedArgs.ChangeType);

                string expectedCreatedAssetName = Guid.NewGuid().ToString();
                assetFileMonitor.ObserveAssets(expectedDeviceName, expectedEndpointName);

                // Create a new asset
                File.WriteAllText(devicePath, expectedCreatedAssetName);
                var assetChangedEventArgs = await assetChangedEventArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Equal(expectedDeviceName, assetChangedEventArgs.DeviceName);
                Assert.Equal(expectedEndpointName, assetChangedEventArgs.InboundEndpointName);
                Assert.Equal(expectedCreatedAssetName, assetChangedEventArgs.AssetName);
                Assert.Equal(AssetFileMonitorChangeType.Created, assetChangedEventArgs.ChangeType);

                // Delete the asset
                assetChangedEventArgsTcs = new();
                File.WriteAllText(devicePath, "");
                var assetDeletedEventArgs = await assetChangedEventArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Equal(expectedDeviceName, assetDeletedEventArgs.DeviceName);
                Assert.Equal(expectedEndpointName, assetDeletedEventArgs.InboundEndpointName);
                Assert.Equal(expectedCreatedAssetName, assetDeletedEventArgs.AssetName);
                Assert.Equal(AssetFileMonitorChangeType.Deleted, assetDeletedEventArgs.ChangeType);

                deviceChangedEventArgsTcs = new();
                File.Delete(devicePath);

                var deviceDeletedArgs = await deviceChangedEventArgsTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.Equal(expectedDeviceName, deviceDeletedArgs.DeviceName);
                Assert.Equal(expectedEndpointName, deviceDeletedArgs.InboundEndpointName);
                Assert.Equal(AssetFileMonitorChangeType.Deleted, deviceDeletedArgs.ChangeType);
            }
            finally
            {
                assetFileMonitor.UnobserveAll();

                try
                {
                    File.Delete(devicePath);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}
