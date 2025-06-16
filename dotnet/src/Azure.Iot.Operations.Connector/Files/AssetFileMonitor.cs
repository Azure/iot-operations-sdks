﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Files.FileMonitor;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using System.Collections.Concurrent;
using System.Text;

namespace Azure.Iot.Operations.Connector.Files
{
    /// <summary>
    /// This class allows for getting and monitor changes to assets and devices.
    /// </summary>
    /// <remarks>
    /// This class is only applicable for connector applications that have been deployed by the Akri operator.
    /// </remarks>
    internal class AssetFileMonitor : IAssetFileMonitor
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
        public event EventHandler<AssetFileChangedEventArgs>? AssetFileChanged;

        /// </inheritdoc>
        public event EventHandler<DeviceFileChangedEventArgs>? DeviceFileChanged;

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
            FilesMonitor assetMonitor = new(_adrResourcesNameMountPath, assetFileName);
            if (_assetFileMonitors.TryAdd(assetFileName, assetMonitor))
            {
                assetMonitor.OnFileChanged += (sender, args) =>
                {
                    if (args.ChangeType == WatcherChangeTypes.Changed)
                    {
                        // Asset names may have changed. Compare new asset names with last known asset names for this device + inbound endpoint
                        IEnumerable<string>? currentAssetNames = GetAssetNames(deviceName, inboundEndpointName);

                        List<string> newAssetNames = new();
                        List<string> removedAssetNames = new();

                        _lastKnownAssetNames.TryGetValue(assetFileName, out List<string>? lastKnownAssetNames);

                        foreach (string currentAssetName in currentAssetNames)
                        {
                            if (lastKnownAssetNames == null || !lastKnownAssetNames.Contains(currentAssetName))
                            {
                                newAssetNames.Add(currentAssetName);
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
                            if (!_lastKnownAssetNames.ContainsKey(assetFileName))
                            {
                                _lastKnownAssetNames[assetFileName] = new();
                            }

                            _lastKnownAssetNames[assetFileName].Add(addedAssetName);
                            AssetFileChanged?.Invoke(this, new(deviceName, inboundEndpointName, addedAssetName, FileChangeType.Created));
                        }

                        foreach (string removedAssetName in removedAssetNames)
                        {
                            if (_lastKnownAssetNames.ContainsKey(assetFileName))
                            {
                                _lastKnownAssetNames[assetFileName].Remove(removedAssetName);
                                AssetFileChanged?.Invoke(this, new(deviceName, inboundEndpointName, removedAssetName, FileChangeType.Deleted));
                            }
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
                        if (!_lastKnownAssetNames.ContainsKey(assetFileName))
                        {
                            _lastKnownAssetNames[assetFileName] = new();
                        }

                        _lastKnownAssetNames[assetFileName].Add(currentAssetName);

                        AssetFileChanged?.Invoke(this, new(deviceName, inboundEndpointName, currentAssetName, FileChangeType.Created));
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
            if (_deviceDirectoryMonitor == null)
            {
                _deviceDirectoryMonitor = new(_adrResourcesNameMountPath, null);
                _deviceDirectoryMonitor.OnFileChanged += (sender, args) =>
                {
                    if (args.FileName.Contains("_") && args.FileName.Split("_").Length == 2)
                    {
                        string deviceName = args.FileName.Split("_")[0];
                        string inboundEndpointName = args.FileName.Split("_")[1];

                        if (args.ChangeType == WatcherChangeTypes.Created)
                        {
                            DeviceFileChanged?.Invoke(this, new(deviceName, inboundEndpointName, FileChangeType.Created));
                        }
                        else if (args.ChangeType == WatcherChangeTypes.Deleted)
                        {
                            DeviceFileChanged?.Invoke(this, new(deviceName, inboundEndpointName, FileChangeType.Deleted));
                        }
                    }
                };

                _deviceDirectoryMonitor.Start();

                // Treat any devices created before this call as newly created
                IEnumerable<string>? currentDeviceNames = GetCompositeDeviceNames();
                if (currentDeviceNames != null)
                {
                    foreach (string deviceName in currentDeviceNames)
                    {
                        DeviceFileChanged?.Invoke(this, new(deviceName.Split('_')[0], deviceName.Split('_')[1], FileChangeType.Created));
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
            if (File.Exists(devicePath))
            {
                return GetMountedConfigurationValueAsLines(devicePath);
            }

            return new List<string>();
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetInboundEndpointNames(string deviceName)
        {
            List<string> inboundEndpointNames = new();

            if (Directory.Exists(_adrResourcesNameMountPath))
            {
                string[] files = Directory.GetFiles(_adrResourcesNameMountPath);
                foreach (string fileNameWithPath in files)
                {
                    string fileName = Path.GetFileName(fileNameWithPath);
                    if (fileName.Contains("_") && fileName.Split("_").Length == 2)
                    {
                        string[] fileNameParts = Path.GetFileName(fileNameWithPath).Split('_');
                        if (fileNameParts[0].Equals(deviceName))
                        {
                            inboundEndpointNames.Add(fileNameParts[1]);
                        }
                    }
                }
            }

            return inboundEndpointNames;
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetDeviceNames()
        {
            HashSet<string> deviceNames = new(); // A device name can appear more than once when searching files, so don't use a list here.

            if (Directory.Exists(_adrResourcesNameMountPath))
            {
                string[] files = Directory.GetFiles(_adrResourcesNameMountPath);
                foreach (string fileNameWithPath in files)
                {
                    string fileName = Path.GetFileName(fileNameWithPath);
                    if (fileName.Contains("_") && fileName.Split("_").Length == 2)
                    {
                        deviceNames.Add(fileName.Split('_')[0]);
                    }
                }
            }

            return deviceNames;
        }

        private IEnumerable<string> GetCompositeDeviceNames()
        {
            HashSet<string> compositeDeviceNames = new(); // A device name can appear more than once when searching files, so don't use a list here.

            if (Directory.Exists(_adrResourcesNameMountPath))
            {
                string[] files = Directory.GetFiles(_adrResourcesNameMountPath);
                foreach (string fileNameWithPath in files)
                {
                    string fileName = Path.GetFileName(fileNameWithPath);
                    if (fileName.Contains("_") && fileName.Split("_").Length == 2)
                    {
                        compositeDeviceNames.Add(fileName);
                    }
                }
            }

            return compositeDeviceNames;
        }

        /// <inheritdoc/>
        public EndpointCredentials GetEndpointCredentials(InboundEndpointSchemaMapValue inboundEndpoint)
        {
            EndpointCredentials deviceCredentials = new();

            if (inboundEndpoint.Authentication != null && inboundEndpoint.Authentication.UsernamePasswordCredentials != null)
            {
                if (inboundEndpoint.Authentication.UsernamePasswordCredentials.UsernameSecretName != null)
                {
                    deviceCredentials.Username = _deviceEndpointCredentialsMountPath != null ? GetMountedConfigurationValueAsString(Path.Combine(_deviceEndpointCredentialsMountPath, inboundEndpoint.Authentication.UsernamePasswordCredentials.UsernameSecretName)) : null;
                }

                if (inboundEndpoint.Authentication.UsernamePasswordCredentials.PasswordSecretName != null)
                {
                    deviceCredentials.Password = _deviceEndpointCredentialsMountPath != null ? GetMountedConfigurationValueAsString(Path.Combine(_deviceEndpointCredentialsMountPath, inboundEndpoint.Authentication.UsernamePasswordCredentials.PasswordSecretName)) : null;
                }
            }

            if (inboundEndpoint.Authentication != null
                && inboundEndpoint.Authentication.X509Credentials != null
                && inboundEndpoint.Authentication.X509Credentials.CertificateSecretName != null)
            {
                deviceCredentials.ClientCertificate = _deviceEndpointCredentialsMountPath != null ? GetMountedConfigurationValueAsString(Path.Combine(_deviceEndpointCredentialsMountPath, inboundEndpoint.Authentication.X509Credentials.CertificateSecretName)) : null;
            }

            //TODO CA certificate retrieval has not been set in stone yet

            return deviceCredentials;
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

        private static IEnumerable<string> GetMountedConfigurationValueAsLines(string path)
        {
            if (!File.Exists(path))
            {
                return new List<string>();
            }

            return FileUtilities.ReadFileLinesWithRetry(path);
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
