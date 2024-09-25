// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    public class AzureDeviceRegistryClient : IDisposable
    {
        // The operator will deploy the connector pod with these environment variables set.
        internal const string ConfigMapMountPathEnvVar = "CONFIGMAP_MOUNT_PATH";
        internal const string AepUsernameSecretMountPathEnvVar = "AEP_USERNAME_SECRET_MOUNT_PATH";
        internal const string AepPasswordSecretMountPathEnvVar = "AEP_PASSWORD_SECRET_MOUNT_PATH";
        internal const string AepCertMountPathEnvVar = "AEP_CERT_MOUNT_PATH";

        // The operator will deploy the connector pod with volumes with this information.
        // These particular files will be in the configmap mount path folder
        string AepTargetAddressRelativeMountPath = "AEP_TARGET_ADDRESS";
        string AepAuthenticationMethodRelativeMountPath = "AEP_AUTHENTICATION_METHOD";
        string AepUsernameSecretNameRelativeMountPath = "AEP_USERNAME_SECRET_NAME";
        string AepPasswordSecretNameRelativeMountPath = "AEP_PASSWORD_SECRET_NAME";
        string EndpointProfileTypeRelativeMountPath = "ENDPOINT_PROFILE_TYPE";
        string AepAdditionalConfigurationRelativeMountPath = "AEP_ADDITIONAL_CONFIGURATION";

        private string _configMapMountPath;
        private string _aepUsernameSecretMountPath;
        private string _aepPasswordSecretMountPath;
        private string _aepCertMountPath;

        FileSystemWatcher assetEndpointProfileFilesSystemWatcher = new();
        FileSystemWatcher assetFilesSystemWatcher = new();

#pragma warning disable CS0067 // Unused for now
        public event EventHandler<Asset>? AssetFileChanged;
        public event EventHandler<AssetEndpointProfile>? AssetEndpointProfileFileChanged;
#pragma warning restore CS0067 // Unused for now

        public AzureDeviceRegistryClient()
        {
            _configMapMountPath = Environment.GetEnvironmentVariable(ConfigMapMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            _aepUsernameSecretMountPath = Environment.GetEnvironmentVariable(AepUsernameSecretMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            _aepPasswordSecretMountPath = Environment.GetEnvironmentVariable(AepPasswordSecretMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            _aepCertMountPath = Environment.GetEnvironmentVariable(AepCertMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
        }

        /// <summary>
        /// Get the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset to retrieve.</param>
        /// <returns>The requested asset.</returns>
        public Task<Asset> GetAssetAsync(string assetId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the asset endpoint profile of the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset whose endpoint profile to retrieve.</param>
        /// <returns>The requested asset endpoint profile.</returns>
        public async Task<AssetEndpointProfile> GetAssetEndpointProfileAsync(string assetId)
        {
            string? aepUsernameSecretName = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepUsernameSecretNameRelativeMountPath}");
            string? aepPasswordSecretName = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepPasswordSecretNameRelativeMountPath}");
            string? aepUsernameSecretFileContents = await GetMountedConfigurationValueAsStringAsync($"{_aepUsernameSecretMountPath}/{aepUsernameSecretName}");
            byte[]? aepPasswordSecretFileContents = await GetMountedConfigurationValueAsync($"{_aepPasswordSecretMountPath}/{aepPasswordSecretName}");
            string? aepCertFileContents = await GetMountedConfigurationValueAsStringAsync(_aepCertMountPath);

            X509Certificate2? aepCert = null;
            if (aepCertFileContents != null)
            {
                //TODO this is a PEM file, right?
                aepCert = X509Certificate2.CreateFromPemFile(aepCertFileContents);
            }

            var credentials = new AssetEndpointProfileCredentials(aepUsernameSecretFileContents, aepPasswordSecretFileContents, aepCert);

            string aepTargetAddressFileContents = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepTargetAddressRelativeMountPath}") ?? throw new InvalidOperationException("TODO");
            string aepAuthenticationMethodFileContents = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepAuthenticationMethodRelativeMountPath}") ?? throw new InvalidOperationException("TODO");
            string endpointProfileTypeFileContents = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{EndpointProfileTypeRelativeMountPath}") ?? throw new InvalidOperationException("TODO");
            string? aepAdditionalConfigurationFileContents = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepAdditionalConfigurationRelativeMountPath}");

            JsonDocument? aepAdditionalConfigurationJson = null;
            if (aepAdditionalConfigurationFileContents != null)
            {
                try
                {
                    aepAdditionalConfigurationJson = JsonDocument.Parse(aepAdditionalConfigurationFileContents);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Unparsable json found in the AEP additional configuration file", e);
                }
            }

            return new AssetEndpointProfile(aepTargetAddressFileContents, aepAuthenticationMethodFileContents, endpointProfileTypeFileContents)
            {
                AdditionalConfiguration = aepAdditionalConfigurationJson,
                Credentials = credentials,
            };
        }

        /// <summary>
        /// Start receiving notifications on <see cref="AssetFileChanged"/> when the asset with the provided Id changes.
        /// </summary>
        /// <param name="assetId">The Id of the asset to observe.</param>
        public void ObserveAsset(string assetId)
        {
            if (!assetFilesSystemWatcher.Filters.Contains("TODO"))
            {
                assetFilesSystemWatcher.Changed += OnAssetFileChanged;

                assetFilesSystemWatcher.EnableRaisingEvents = true;
            }

        }

        /// <summary>
        /// Stop receiving notifications on <see cref="AssetFileChanged"/> when the asset with the provided Id changes.
        /// </summary>
        /// <param name="assetId">The Id of the asset to unobserve.</param>
        public void UnobserveAsset(string assetId)
        {
            if (assetFilesSystemWatcher.Filters.Contains("TODO"))
            {
                assetFilesSystemWatcher.Changed -= OnAssetFileChanged;

                assetFilesSystemWatcher.EnableRaisingEvents = false;
            }
        }

        /// <summary>
        /// Start receiving notifications on <see cref="AssetEndpointProfileFileChanged"/> when the asset endpoint profile
        /// changes for the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset whose endpoint profile you want to observe.</param>
        public void ObserveAssetEndpointProfile(string assetId)
        {
            if (!assetEndpointProfileFilesSystemWatcher.Filters.Contains(_aepUsernameSecretMountPath))
            {
                assetEndpointProfileFilesSystemWatcher.Filters.Add($"{_configMapMountPath}/{AepTargetAddressRelativeMountPath}");
                assetEndpointProfileFilesSystemWatcher.Filters.Add($"{_configMapMountPath}/{AepAuthenticationMethodRelativeMountPath}");
                assetEndpointProfileFilesSystemWatcher.Filters.Add($"{_configMapMountPath}/{EndpointProfileTypeRelativeMountPath}");
                assetEndpointProfileFilesSystemWatcher.Filters.Add($"{_configMapMountPath}/{AepAdditionalConfigurationRelativeMountPath}");

                assetEndpointProfileFilesSystemWatcher.Filters.Add(_aepUsernameSecretMountPath);
                assetEndpointProfileFilesSystemWatcher.Filters.Add(_aepPasswordSecretMountPath);
                assetEndpointProfileFilesSystemWatcher.Filters.Add(_aepCertMountPath);

                assetEndpointProfileFilesSystemWatcher.Changed += OnAssetEndpointProfileFileChanged;

                assetEndpointProfileFilesSystemWatcher.EnableRaisingEvents = true;
            }
        }

        /// <summary>
        /// Stop receiving notifications on <see cref="AssetEndpointProfileFileChanged"/> when the asset endpoint profile
        /// changes for the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset whose endpoint profile you want to unobserve.</param>
        public void UnobserveAssetEndpointProfile(string assetId)
        {
            if (assetEndpointProfileFilesSystemWatcher.Filters.Contains(_aepUsernameSecretMountPath))
            {
                assetEndpointProfileFilesSystemWatcher.Filters.Remove($"{_configMapMountPath}/{AepTargetAddressRelativeMountPath}");
                assetEndpointProfileFilesSystemWatcher.Filters.Remove($"{_configMapMountPath}/{AepAuthenticationMethodRelativeMountPath}");
                assetEndpointProfileFilesSystemWatcher.Filters.Remove($"{_configMapMountPath}/{EndpointProfileTypeRelativeMountPath}");
                assetEndpointProfileFilesSystemWatcher.Filters.Remove($"{_configMapMountPath}/{AepAdditionalConfigurationRelativeMountPath}");

                assetEndpointProfileFilesSystemWatcher.Filters.Remove(_aepUsernameSecretMountPath);
                assetEndpointProfileFilesSystemWatcher.Filters.Remove(_aepPasswordSecretMountPath);
                assetEndpointProfileFilesSystemWatcher.Filters.Remove(_aepCertMountPath);

                assetEndpointProfileFilesSystemWatcher.Changed -= OnAssetEndpointProfileFileChanged;

                assetEndpointProfileFilesSystemWatcher.EnableRaisingEvents = false;
            }
        }

        /// <summary>
        /// Returns the complete list of assets deployed by the operator to this pod.
        /// </summary>
        /// <returns>The complete list of assets deployed by the operator to this pod.</returns>
        public IEnumerable<string> GetAssetIds()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Dispose this client and all its resources.
        /// </summary>
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private void OnAssetEndpointProfileFileChanged(object sender, FileSystemEventArgs e)
        {
            new Task(async () =>
            {
                switch (e.ChangeType)
                {
                    case WatcherChangeTypes.Changed:
                        AssetEndpointProfileFileChanged?.Invoke(this, await GetAssetEndpointProfileAsync());
                        break;
                    case WatcherChangeTypes.Created:
                    case WatcherChangeTypes.Renamed:
                    case WatcherChangeTypes.Deleted:
                    default:
                        // This would only happen if the user is messing around with these files for some reason. Under
                        // normal conditions, the credentials files should only ever be updated in place with new credentials
                        Trace.TraceWarning("One or more asset endpoint profile credentials files was renamed/deleted/created unexpectedly");
                        break;
                }
            }).Start();
        }

        private void OnAssetFileChanged(object sender, FileSystemEventArgs e)
        {
            new Task(async () =>
            {
                switch (e.ChangeType)
                {
                    case WatcherChangeTypes.Changed:
                        AssetFileChanged?.Invoke(this, await GetAssetAsync());
                        break;
                    case WatcherChangeTypes.Created:
                    case WatcherChangeTypes.Renamed:
                    case WatcherChangeTypes.Deleted:
                    default:
                        // This would only happen if the user is messing around with these files for some reason. Under
                        // normal conditions, the asset files should only ever be updated in place
                        Trace.TraceWarning("One or more asset files was renamed/deleted/created unexpectedly");
                        break;
                }
            }).Start();
        }

        private async static Task<string?> GetMountedConfigurationValueAsStringAsync(string path)
        {
            byte[]? bytesRead = await GetMountedConfigurationValueAsync(path);

            return bytesRead != null ? Encoding.UTF8.GetString(bytesRead) : null;
        }

        private async static Task<byte[]?> GetMountedConfigurationValueAsync(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return await File.ReadAllBytesAsync(path);
        }
    }
}
