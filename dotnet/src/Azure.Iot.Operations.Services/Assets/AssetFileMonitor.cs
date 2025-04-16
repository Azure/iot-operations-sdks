﻿// Copyright (c) Microsoft Corporation.
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
    public class AssetFileMonitor : IAssetFileMonitor // TODO should this be public?
    {
        // Environment variables set by operator when connector is deployed
        internal const string AdrResourcesNameMountPathEnvVar = "ADR_RESOURCES_NAME_MOUNT_PATH";
        internal const string DeviceEndpointTlsTrustBundleCertMountPathEnvVar = "DEVICE_ENDPOINT_TLS_TRUST_BUNDLE_CA_CERT_MOUNT_PATH";
        internal const string DeviceEndpointCredentialsMountPathEnvVar = "DEVICE_ENDPOINT_CREDENTIALS_MOUNT_PATH";

        private readonly string _adrResourcesNameMountPath;
        private readonly string? _deviceEndpointTlsTrustBundleCertMountPath;
        private readonly string? _deviceEndpointCredentialsMountPath;

        // Key is <deviceName>_<inboundEndpointName>, value is list of asset names in that file
        private readonly ConcurrentDictionary<string, List<string>> _lastKnownAssetNames = new();

        // Key is <deviceName>_<inboundEndpointName>, value is the file watcher for the asset
        private readonly ConcurrentDictionary<string, FilesMonitor> _assetFileMonitors = new();

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
                assetMonitor.OnFileChanged += (sender, args) =>
                {
                    if (args.ChangeType == WatcherChangeTypes.Changed)
                    {
                        // Asset names may have changed. Compare new asset names with last known asset names for this device + inbound endpoint
                        IEnumerable<string>? currentAssetNames = GetAssetNames(deviceName, inboundEndpointName);

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

                // Treate any assets that already exist as though they were just created
                IEnumerable<string>? currentAssetNames = GetAssetNames(deviceName, inboundEndpointName);
                if (currentAssetNames != null)
                {
                    foreach (string currentAssetName in currentAssetNames)
                    {
                        AssetCreated?.Invoke(this, new(deviceName, inboundEndpointName, currentAssetName));
                    }
                }
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
            if (_deviceDirectoryMonitor != null)
            {
                _deviceDirectoryMonitor = new(_adrResourcesNameMountPath, null);
                _deviceDirectoryMonitor.OnFileChanged += (sender, args) =>
                {
                    string deviceName = args.FileName.Split("_")[0];
                    string inboundEndpointName = args.FileName.Split("_")[1];

                    if (args.ChangeType == WatcherChangeTypes.Created)
                    {
                        DeviceCreated?.Invoke(this, new(deviceName, inboundEndpointName));
                    }
                    else if (args.ChangeType == WatcherChangeTypes.Deleted)
                    {
                        DeviceDeleted?.Invoke(this, new(deviceName, inboundEndpointName));
                    }
                };

                _deviceDirectoryMonitor.Start();

                // Treat any devices created before this call as newly created
                IEnumerable<string>? currentDeviceNames = GetDeviceNames();
                if (currentDeviceNames != null)
                {
                    foreach (string deviceName in currentDeviceNames)
                    {
                        DeviceCreated?.Invoke(this, new(deviceName.Split('_')[0], deviceName.Split('_')[1]));
                    }
                }
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
        public IEnumerable<string>? GetAssetNames(string deviceName, string inboundEndpointName)
        {
            List<string>? deviceNames = null;

            string devicePath = Path.Combine(_adrResourcesNameMountPath, $"{deviceName}_{inboundEndpointName}");
            if (Directory.Exists(devicePath))
            {
                string? contents = GetMountedConfigurationValueAsString(devicePath);
                if (contents == null)
                {
                    return null;
                }

                string[] delimitedContents = contents.Split(";");
                return [.. delimitedContents];
            }

            return deviceNames;
        }

        public IEnumerable<string>? GetDeviceNames() // {<deviceName>_<inboundEndpointName>}
        {
            List<string>? deviceNames = null;

            if (Directory.Exists(AdrResourcesNameMountPathEnvVar))
            {
                string[] files = Directory.GetFiles(AdrResourcesNameMountPathEnvVar);
                foreach (string fileNameWithPath in files)
                {
                    deviceNames ??= new();
                    deviceNames.Add(Path.GetFileName(fileNameWithPath));
                }
            }

            return deviceNames;
        }

        public void UnobserveAll()
        {
            _deviceDirectoryMonitor?.Stop();
            foreach (var assetMonitor in _assetFileMonitors.Values)
            {
                assetMonitor.Stop();
            }
        }

        private static string? GetMountedConfigurationValueAsString(string path)
        {
            byte[]? bytesRead = GetMountedConfigurationValue(path);
            return bytesRead != null ? Encoding.UTF8.GetString(bytesRead) : null;
        }

        private static byte[]? GetMountedConfigurationValue(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return FileUtilities.ReadFileWithRetry(path);
        }
    }
}
