﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Assets.FileMonitor;
using System.Collections.Concurrent;
using System.Text;

namespace Azure.Iot.Operations.Connector.Assets
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
        private readonly string _deviceEndpointTlsTrustBundleCertMountPath;
        private readonly string _deviceEndpointCredentialsMountPath;

        // Key is <deviceName>_<inboundEndpointName>, value is list of asset names in that file
        private readonly ConcurrentDictionary<string, List<string>> _lastKnownAssetNames = new();

        // Key is <deviceName>_<inboundEndpointName>, value is the file watcher for the asset
        private readonly ConcurrentDictionary<string, FilesMonitor> _assetFileMonitors = new();

        private FilesMonitor? _deviceDirectoryMonitor;

        /// </inheritdoc>
        public event EventHandler<AssetChangedEventArgs>? AssetFileChanged;

        /// </inheritdoc>
        public event EventHandler<DeviceChangedEventArgs>? DeviceFileChanged;

        public AssetFileMonitor()
        {
            _adrResourcesNameMountPath = Environment.GetEnvironmentVariable(AdrResourcesNameMountPathEnvVar) ?? throw new InvalidOperationException($"Missing {AdrResourcesNameMountPathEnvVar} environment variable");
            _deviceEndpointTlsTrustBundleCertMountPath = Environment.GetEnvironmentVariable(DeviceEndpointTlsTrustBundleCertMountPathEnvVar) ?? throw new InvalidOperationException($"Missing {DeviceEndpointTlsTrustBundleCertMountPathEnvVar} environment variable");
            _deviceEndpointCredentialsMountPath = Environment.GetEnvironmentVariable(DeviceEndpointCredentialsMountPathEnvVar) ?? throw new InvalidOperationException($"Missing {DeviceEndpointCredentialsMountPathEnvVar} environment variable");
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
                            AssetFileChanged?.Invoke(this, new(deviceName, inboundEndpointName, addedAssetName, AssetFileMonitorChangeType.Created));
                        }

                        foreach (string removedAssetName in removedAssetNames)
                        {
                            _lastKnownAssetNames[assetFileName].Remove(removedAssetName);
                            AssetFileChanged?.Invoke(this, new(deviceName, inboundEndpointName, removedAssetName, AssetFileMonitorChangeType.Deleted));
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
                        AssetFileChanged?.Invoke(this, new(deviceName, inboundEndpointName, currentAssetName, AssetFileMonitorChangeType.Created));
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
                        DeviceFileChanged?.Invoke(this, new(deviceName, inboundEndpointName, AssetFileMonitorChangeType.Created));
                    }
                    else if (args.ChangeType == WatcherChangeTypes.Deleted)
                    {
                        DeviceFileChanged?.Invoke(this, new(deviceName, inboundEndpointName, AssetFileMonitorChangeType.Deleted));
                    }
                };

                _deviceDirectoryMonitor.Start();

                // Treat any devices created before this call as newly created
                IEnumerable<string>? currentDeviceNames = GetDeviceNames();
                if (currentDeviceNames != null)
                {
                    foreach (string deviceName in currentDeviceNames)
                    {
                        DeviceFileChanged?.Invoke(this, new(deviceName.Split('_')[0], deviceName.Split('_')[1], AssetFileMonitorChangeType.Created));
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
        public IEnumerable<string> GetAssetNames(string deviceName, string inboundEndpointName)
        {
            string devicePath = Path.Combine(_adrResourcesNameMountPath, $"{deviceName}_{inboundEndpointName}");
            if (Directory.Exists(devicePath))
            {
                string? contents = GetMountedConfigurationValueAsString(devicePath);
                if (contents != null)
                {
                    string[] delimitedContents = contents.Split(";");
                    return [.. delimitedContents];
                }
            }

            return new List<string>();
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetInboundEndpointNames(string deviceName)
        {
            List<string> inboundEndpointNames = new();

            if (Directory.Exists(AdrResourcesNameMountPathEnvVar))
            {
                string[] files = Directory.GetFiles(AdrResourcesNameMountPathEnvVar);
                foreach (string fileNameWithPath in files)
                {
                    string[] fileNameParts = Path.GetFileName(fileNameWithPath).Split('_');
                    if (fileNameParts[0].Equals(deviceName))
                    {
                        inboundEndpointNames.Add(fileNameParts[1]);
                    }
                }
            }

            return inboundEndpointNames;
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetDeviceNames()
        {
            HashSet<string> deviceNames = new(); // A device name can appear more than once when searching files, so don't use a list here.

            if (Directory.Exists(AdrResourcesNameMountPathEnvVar))
            {
                string[] files = Directory.GetFiles(AdrResourcesNameMountPathEnvVar);
                foreach (string fileNameWithPath in files)
                {
                    deviceNames.Add(Path.GetFileName(fileNameWithPath).Split('_')[0]);
                }
            }

            return deviceNames;
        }

        /// <inheritdoc/>
        public DeviceCredentials GetDeviceCredentials(string deviceName, string inboundEndpointName)
        {
            string fileName = $"{deviceName}_{inboundEndpointName}";
            string? username = GetMountedConfigurationValueAsString(Path.Combine(_deviceEndpointCredentialsMountPath, fileName + "_username"));
            byte[]? password = GetMountedConfigurationValue(Path.Combine(_deviceEndpointCredentialsMountPath, fileName + "_password"));
            string? clientCertificate = GetMountedConfigurationValueAsString(Path.Combine(_deviceEndpointCredentialsMountPath, fileName + "_certificate"));
            string? caCert = GetMountedConfigurationValueAsString(Path.Combine(_deviceEndpointTlsTrustBundleCertMountPath, fileName));

            return new DeviceCredentials()
            {
                Username = username,
                Password = password,
                ClientCertificate = clientCertificate,
                CaCertificate = caCert
            };
        }

        /// <inheritdoc/>
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
