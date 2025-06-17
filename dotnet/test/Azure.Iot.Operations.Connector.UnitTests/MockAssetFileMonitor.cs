// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    public class MockAssetFileMonitor : IAssetFileMonitor
    {
        private readonly Dictionary<string, List<string>> _devicesAndAssetNames = new();
        private readonly List<string> _observedDevicesAndAssetNames = new();

        private bool _isObservingDevices = false;

        public event EventHandler<AssetFileChangedEventArgs>? AssetFileChanged;
        public event EventHandler<DeviceFileChangedEventArgs>? DeviceFileChanged;

        public void SimulateNewDeviceCreated(string deviceName, string inboundEndpointName)
        {
            if (_isObservingDevices)
            {
                DeviceFileChanged?.Invoke(this, new(deviceName, inboundEndpointName, FileChangeType.Created));
            }

            _devicesAndAssetNames.Add(ToComposite(deviceName, inboundEndpointName), new());
        }

        public void SimulateNewDeviceDeleted(string deviceName, string inboundEndpointName)
        {
            if (_isObservingDevices)
            {
                DeviceFileChanged?.Invoke(this, new(deviceName, inboundEndpointName, FileChangeType.Deleted));
            }

            _devicesAndAssetNames.Remove(ToComposite(deviceName, inboundEndpointName));
        }

        public void SimulateNewAssetCreated(string deviceName, string inboundEndpointName, string assetName)
        {
            if (_observedDevicesAndAssetNames.Contains(ToComposite(deviceName, inboundEndpointName)))
            {
                AssetFileChanged?.Invoke(this, new(deviceName, inboundEndpointName, assetName, FileChangeType.Created));
            }

            _devicesAndAssetNames[ToComposite(deviceName, inboundEndpointName)].Add(assetName);
        }

        public void SimulateNewAssetDeleted(string deviceName, string inboundEndpointName, string assetName)
        {
            if (_observedDevicesAndAssetNames.Contains(ToComposite(deviceName, inboundEndpointName)))
            {
                AssetFileChanged?.Invoke(this, new(deviceName, inboundEndpointName, assetName, FileChangeType.Deleted));
            }

            _devicesAndAssetNames[ToComposite(deviceName, inboundEndpointName)].Remove(assetName);
        }

        public void ObserveAssets(string deviceName, string inboundEndpointName)
        {
            _observedDevicesAndAssetNames.Add($"{deviceName}_{inboundEndpointName}");
        }

        public void UnobserveAssets(string deviceName, string inboundEndpointName)
        {
            _observedDevicesAndAssetNames.Remove($"{deviceName}_{inboundEndpointName}");
        }

        public IEnumerable<string> GetAssetNames(string deviceName, string inboundEndpointName)
        {
            return _devicesAndAssetNames.Keys.Contains(ToComposite(deviceName, inboundEndpointName)) ? _devicesAndAssetNames[ToComposite(deviceName, inboundEndpointName)] : new List<string>();
        }

        public IEnumerable<string> GetInboundEndpointNames(string deviceName)
        {
            HashSet<string> deviceNames = new();
            foreach (string compositeName in _devicesAndAssetNames.Keys)
            {
                deviceNames.Add(compositeName.Split("_")[1]);
            }

            return deviceNames;
        }

        public IEnumerable<string> GetDeviceNames()
        {
            HashSet<string> deviceNames = new();
            foreach (string compositeName in _devicesAndAssetNames.Keys)
            {
                deviceNames.Add(compositeName.Split("_")[0]);
            }

            return deviceNames;
        }

        public void ObserveDevices()
        {
            _isObservingDevices = true;
        }

        public void UnobserveDevices()
        {
            _isObservingDevices = false;
        }

        public void UnobserveAll()
        {
            _isObservingDevices = false;
            _devicesAndAssetNames.Clear();
        }

        private string ToComposite(string deviceName, string inboundEndpointName)
        {
            return $"{deviceName}_{inboundEndpointName}";
        }

        public EndpointCredentials GetEndpointCredentials(string deviceName, string inboundEndpointName, InboundEndpointSchemaMapValue inboundEndpoint)
        {
            throw new NotImplementedException();
        }
    }
}
