// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.Files.FilesMonitor;
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
    public class AssetFileMonitor : IAssetFileMonitor
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
        private readonly ConcurrentDictionary<string, IFilesMonitor> _assetFileMonitors = new();

        private IFilesMonitor? _deviceDirectoryMonitor;

        private readonly IFilesMonitorFactory _filesMonitorFactory;

        /// </inheritdoc>
        public event EventHandler<AssetFileChangedEventArgs>? AssetFileChanged;

        /// </inheritdoc>
        public event EventHandler<DeviceFileChangedEventArgs>? DeviceFileChanged;

        /// <summary>
        /// Instantiate this class with the provided style of file monitor.
        /// </summary>
        /// <param name="filesMonitorFactory">
        /// The factory to provide all file monitors used to watch for device and/or asset changes. If not provided, an
        /// instance of <see cref="FsnotifyFilesMonitorFactory"/> will be used. For operating systems where .NET's
        /// <see cref="FileSystemWatcher"/> isn't sufficient, users can opt to poll for file changes instead using <see cref="PollingFilesMonitorFactory"/>.
        /// </param>
        public AssetFileMonitor(IFilesMonitorFactory? filesMonitorFactory = null)
        {
            _filesMonitorFactory = filesMonitorFactory ?? new FsnotifyFilesMonitorFactory();
            _adrResourcesNameMountPath = Environment.GetEnvironmentVariable(AdrResourcesNameMountPathEnvVar) ?? throw new InvalidOperationException($"Missing {AdrResourcesNameMountPathEnvVar} environment variable");
            _deviceEndpointTlsTrustBundleCertMountPath = Environment.GetEnvironmentVariable(DeviceEndpointTlsTrustBundleCertMountPathEnvVar);
            _deviceEndpointCredentialsMountPath = Environment.GetEnvironmentVariable(DeviceEndpointCredentialsMountPathEnvVar);
        }

        /// <inheritdoc/>
        public void ObserveAssets(string deviceName, string inboundEndpointName)
        {
            string assetFileName = $"{deviceName}_{inboundEndpointName}";
            IFilesMonitor assetMonitor = _filesMonitorFactory.Create();
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

                assetMonitor.Start(_adrResourcesNameMountPath, assetFileName);

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
            if (_assetFileMonitors.TryRemove(assetFileName, out IFilesMonitor? assetMonitor))
            {
                assetMonitor.Stop();
            }
        }

        /// <inheritdoc/>
        public void ObserveDevices()
        {
            if (_deviceDirectoryMonitor == null)
            {
                _deviceDirectoryMonitor = _filesMonitorFactory.Create();
                _deviceDirectoryMonitor.OnFileChanged += (sender, args) =>
                {
                    if (isDeviceFile(args.FileName))
                    {
                        splitCompositeName(Path.GetFileName(args.FileName), out string foundDeviceName, out string foundInboundEndpointName);

                        if (args.ChangeType == WatcherChangeTypes.Created)
                        {
                            DeviceFileChanged?.Invoke(this, new(foundDeviceName, foundInboundEndpointName, FileChangeType.Created));
                        }
                        else if (args.ChangeType == WatcherChangeTypes.Deleted)
                        {
                            DeviceFileChanged?.Invoke(this, new(foundDeviceName, foundInboundEndpointName, FileChangeType.Deleted));
                        }
                    }
                };

                _deviceDirectoryMonitor.Start(_adrResourcesNameMountPath, null);

                // Treat any devices created before this call as newly created
                IEnumerable<string>? currentCompositeDeviceNames = GetCompositeDeviceNames();
                if (currentCompositeDeviceNames != null)
                {
                    foreach (string compositeDeviceName in currentCompositeDeviceNames)
                    {
                        splitCompositeName(compositeDeviceName, out string deviceName, out string inboundEndpointName);

                        DeviceFileChanged?.Invoke(this, new(deviceName, inboundEndpointName, FileChangeType.Created));
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
                    if (isDeviceFile(fileName))
                    {
                        splitCompositeName(Path.GetFileName(fileNameWithPath), out string foundDeviceName, out string foundInboundEndpointName);
                        if (foundDeviceName.Equals(deviceName))
                        {
                            inboundEndpointNames.Add(foundInboundEndpointName);
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
                    if (isDeviceFile(fileName))
                    {
                        splitCompositeName(Path.GetFileName(fileNameWithPath), out string foundDeviceName, out string foundInboundEndpointName);
                        deviceNames.Add(foundDeviceName);
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
                    if (isDeviceFile(fileName))
                    {
                        compositeDeviceNames.Add(fileName);
                    }
                }
            }

            return compositeDeviceNames;
        }

        /// <inheritdoc/>
        public EndpointCredentials GetEndpointCredentials(string deviceName, string inboundEndpointName, InboundEndpointSchemaMapValue inboundEndpoint)
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

            string compositeName = deviceName + "_" + inboundEndpointName;
            if (_deviceEndpointTlsTrustBundleCertMountPath != null
                && File.Exists(Path.Combine(_deviceEndpointTlsTrustBundleCertMountPath, compositeName)))
            {
                deviceCredentials.CaCertificate = GetMountedConfigurationValueAsString(Path.Combine(_deviceEndpointTlsTrustBundleCertMountPath, compositeName));
            }
            
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

        // composite name follows the shape "<deviceName>_<inboundEndpointName>" where device name cannot have an underscore, but inboundEndpointName
        // may contain 0 to many underscores.
        private void splitCompositeName(string compositeName, out string deviceName, out string inboundEndpointName)
        {
            int indexOfFirstUnderscore = compositeName.IndexOf('_');
            if (indexOfFirstUnderscore == -1)
            {
                deviceName = compositeName;
                inboundEndpointName = "";
                return;
            }

            deviceName = compositeName.Substring(0, indexOfFirstUnderscore);
            inboundEndpointName = compositeName.Substring(indexOfFirstUnderscore + 1);
        }

        private bool isDeviceFile(string fileName)
        {
            // Ignore any symlink files like "..data" and ignore any files that don't look like {deviceName}_{inboundEndpointName}
            return !fileName.StartsWith("..") && fileName.Contains("_");
        }
    }
}
