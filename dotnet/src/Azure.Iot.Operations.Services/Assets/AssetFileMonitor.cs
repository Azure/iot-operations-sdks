// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets.FileMonitor;
using System.Collections.Concurrent;
using System.Text;

namespace Azure.Iot.Operations.Services.Assets
{
    /// <summary>
    /// This class allows for getting and monitor changes to assets and asset endpoint profiles.
    /// </summary>
    /// <remarks>
    /// This class is only applicable for connector applications that have been deployed by the Akri operator.
    /// </remarks>
    public class AssetFileMonitor : IAssetFileMonitor
    {
        // Environment variables set by operator when connector is deployed
        internal const string AdrResourcesNameMountPathEnvVar = "ADR_RESOURCES_NAME_MOUNT_PATH";
        internal const string DeviceEndpointTlsTrustBundleCertMountPathEnvVar = "DEVICE_ENDPOINT_TLS_TRUST_BUNDLE_CA_CERT_MOUNT_PATH";
        internal const string DeviceEndpointCredentialsMountPathEnvVar = "DEVICE_ENDPOINT_CREDENTIALS_MOUNT_PATH";

        private readonly string _adrResourcesNameMountPath;
        private readonly string? _deviceEndpointTlsTrustBundleCertMountPath;
        private readonly string? _deviceEndpointCredentialsMountPath;

        // Key is <deviceName>_<inboundEndpointName>, value is the file watcher for the asset
        private readonly ConcurrentDictionary<string, FilesMonitor> _assetFileMonitors = new();

        // Key is <deviceName>_<inboundEndpointName>, value is list of asset names in that file
        private readonly ConcurrentDictionary<string, List<string>> _lastKnownAssetNames = new();

        private FilesMonitor? _deviceDirectoryMonitor;

        /// </inheritdoc>
        public event EventHandler<AssetCreatedEventArgs>? AssetCreated;

        /// </inheritdoc>
        public event EventHandler<AssetDeletedEventArgs>? AssetDeleted;

        /// </inheritdoc>
        public event EventHandler<DeviceCreatedEventArgs>? DeviceCreated;

        /// </inheritdoc>
        public event EventHandler<DeviceDeletedEventArgs>? DeviceDeleted;

        public AssetFileMonitor()
        {
            _adrResourcesNameMountPath = Environment.GetEnvironmentVariable(AdrResourcesNameMountPathEnvVar) ?? throw new InvalidOperationException($"Missing {AdrResourcesNameMountPathEnvVar} environment variable");
            _deviceEndpointTlsTrustBundleCertMountPath = Environment.GetEnvironmentVariable(DeviceEndpointTlsTrustBundleCertMountPathEnvVar);
            _deviceEndpointCredentialsMountPath = Environment.GetEnvironmentVariable(DeviceEndpointCredentialsMountPathEnvVar);
        }

        /// <inheritdoc/>
        public void ObserveAssets(string deviceName, string inboundEndpointName)
        {
            string assetFileName = $"{deviceName}_{inboundEndpointName}";
            if (!_assetFileMonitors.ContainsKey(assetFileName))
            {
                FilesMonitor assetMonitor = new(_adrResourcesNameMountPath, assetFileName);
                _assetFileMonitors.TryAdd(assetFileName, assetMonitor);
                assetMonitor.OnFileChanged += async (sender, args) =>
                {
                    if (args.ChangeType == WatcherChangeTypes.Changed)
                    {
                        // Asset names may have changed. Compare new asset names with last known asset names for this device + inbound endpoint
                        IEnumerable<string>? currentAssetNames = await GetAssetNamesAsync(deviceName, inboundEndpointName);

                        List<string> newAssetNames = new();
                        List<string> removedAssetNames = new();

                        if (_lastKnownAssetNames.TryGetValue(assetFileName, out List<string>? lastKnownAssetNames) && currentAssetNames != null)
                        {
                            foreach (string currentAssetName in currentAssetNames)
                            {
                                if (!lastKnownAssetNames.Contains(currentAssetName))
                                {
                                    newAssetNames.Add(currentAssetName);
                                }
                            }
                        }

                        if (lastKnownAssetNames != null)
                        {
                            foreach (string lastKnownAssetName in lastKnownAssetNames)
                            {
                                if (currentAssetNames == null || !currentAssetNames.Contains(lastKnownAssetName))
                                {
                                    removedAssetNames.Add(lastKnownAssetName);
                                }
                            }
                        }

                        foreach (string addedAssetName in newAssetNames)
                        {
                            _lastKnownAssetNames[assetFileName].Add(addedAssetName);
                            AssetCreated?.Invoke(this, new(deviceName, addedAssetName));
                        }

                        foreach (string removedAssetName in removedAssetNames)
                        {
                            _lastKnownAssetNames[assetFileName].Remove(removedAssetName);
                            AssetDeleted?.Invoke(this, new(deviceName, removedAssetName));
                        }
                    }
                };

                assetMonitor.Start();
            }
        }

        /// <inheritdoc/>
        public void UnobserveAssets(string deviceName, string inboundEndpointName)
        {
            string assetFileName = $"{deviceName}_{inboundEndpointName}";
            if (_assetFileMonitors.TryRemove(assetFileName, out FilesMonitor? assetMonitor))
            {
                assetMonitor.Stop();
            }
        }

        /// <inheritdoc/>
        public void ObserveDevices()
        {
            //TODO make start check the initial state!

            if (_deviceDirectoryMonitor != null)
            {
                _deviceDirectoryMonitor = new(_adrResourcesNameMountPath, null);
                _deviceDirectoryMonitor.OnFileChanged += (sender, args) =>
                {
                    string deviceName = args.FileName.Split("_")[0];
                    string inboundEndpointName = args.FileName.Split("_")[1]; //TODO what to do with this?

                    if (args.ChangeType == WatcherChangeTypes.Created)
                    {
                        DeviceCreated?.Invoke(this, new(deviceName));
                    }
                    else if (args.ChangeType == WatcherChangeTypes.Deleted)
                    {
                        DeviceDeleted?.Invoke(this, new(deviceName));
                    }
                };

                _deviceDirectoryMonitor.Start();
            }
        }

        /// <inheritdoc/>
        public void UnobserveDevices()
        {
            if (_deviceDirectoryMonitor != null)
            {
                _deviceDirectoryMonitor.Stop();
                _deviceDirectoryMonitor = null;
            }
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<string>?> GetAssetNamesAsync(string deviceName, string inboundEndpointName)
        {
            List<string>? deviceNames = null;

            string devicePath = Path.Combine(_adrResourcesNameMountPath, $"{deviceName}_{inboundEndpointName}");
            if (Directory.Exists(devicePath))
            {
                string contents = await GetMountedConfigurationValueAsStringAsync(devicePath);
                string[] delimitedContents = contents.Split(";");
                return [.. delimitedContents];
            }

            return deviceNames;
        }

        public IEnumerable<string>? GetDeviceNames()
        {
            List<string>? deviceNames = null;

            if (Directory.Exists(AdrResourcesNameMountPathEnvVar))
            {
                string[] files = Directory.GetFiles(AdrResourcesNameMountPathEnvVar);
                foreach (string fileNameWithPath in files)
                {
                    deviceNames ??= new();
                    deviceNames.Add(Path.GetFileName(fileNameWithPath).Split("_")[0]);
                }
            }

            return deviceNames;
        }

        public void UnobserveAll()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private static async Task<string?> GetMountedConfigurationValueAsStringAsync(string path)
        {
            byte[]? bytesRead = await GetMountedConfigurationValueAsync(path);
            return bytesRead != null ? Encoding.UTF8.GetString(bytesRead) : null;
        }

        private static async Task<byte[]?> GetMountedConfigurationValueAsync(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return await FileUtilities.ReadFileWithRetryAsync(path);
        }
    }
}
