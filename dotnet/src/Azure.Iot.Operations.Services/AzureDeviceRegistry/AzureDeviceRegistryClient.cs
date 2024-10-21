// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    public class AzureDeviceRegistryClient
    {
        // The operator will deploy the connector pod with these environment variables set.
        internal const string ConfigMapMountPathEnvVar = "AEP_MQ_CONFIGMAP_MOUNT_PATH";
        internal const string AepUsernameSecretMountPathEnvVar = "AEP_USERNAME_SECRET_MOUNT_PATH";
        internal const string AepPasswordSecretMountPathEnvVar = "AEP_PASSWORD_SECRET_MOUNT_PATH";
        internal const string AepCertMountPathEnvVar = "AEP_CERT_MOUNT_PATH";
        internal const string AssetConfigMapEnvVar = "ASSET_CONFIGMAP_MOUNT_PATH";

        // The operator will deploy the connector pod with volumes with this information.
        // These particular files will be in the configmap mount path folder
        internal string AepTargetAddressRelativeMountPath = "AEP_TARGET_ADDRESS";
        internal string AepAuthenticationMethodRelativeMountPath = "AEP_AUTHENTICATION_METHOD";
        internal string AepUsernameSecretNameRelativeMountPath = "AEP_USERNAME_FILE_NAME";
        internal string AepPasswordSecretNameRelativeMountPath = "AEP_PASSWORD_FILE_NAME";
        internal string AepCertificateSecretNameRelativeMountPath = "AEP_CERT_FILE_NAME";
        internal string EndpointProfileTypeRelativeMountPath = "ENDPOINT_PROFILE_TYPE";
        internal string AepAdditionalConfigurationRelativeMountPath = "AEP_ADDITIONAL_CONFIGURATION";
        internal string AepDiscoveredAssetEndpointProfileRefRelativeMountPath = "AEP_DISCOVERED_ASSET_ENDPOINT_PROFILE_REF";
        internal string AepUuidRelativeMountPath = "AEP_UUID";

        private Dictionary<string, FilesObserver> assetEndpointProfileFileObservers = new();

        private string _assetMapMountPath;
        private string _configMapMountPath;
        private string? _aepUsernameSecretMountPath;
        private string? _aepPasswordSecretMountPath;
        private string? _aepCertMountPath;

#pragma warning disable CS0067 // Unused for now
        public event EventHandler<Asset>? AssetChanged;
#pragma warning restore CS0067 // Unused for now
        public event EventHandler<AssetEndpointProfile>? AssetEndpointProfileChanged;

        public AzureDeviceRegistryClient()
        {
            _assetMapMountPath = Environment.GetEnvironmentVariable(AssetConfigMapEnvVar) ?? throw new InvalidOperationException("Missing the asset config map mount path environment variable");
            _configMapMountPath = Environment.GetEnvironmentVariable(ConfigMapMountPathEnvVar) ?? throw new InvalidOperationException("Missing the AEP config map mount path environment variable");
            _aepUsernameSecretMountPath = Environment.GetEnvironmentVariable(AepUsernameSecretMountPathEnvVar);
            _aepPasswordSecretMountPath = Environment.GetEnvironmentVariable(AepPasswordSecretMountPathEnvVar);
            _aepCertMountPath = Environment.GetEnvironmentVariable(AepCertMountPathEnvVar);
        }

        /// <summary>
        /// Get the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The requested asset.</returns>
        public Task<Asset> GetAssetAsync(string assetId, CancellationToken cancellationToken = default)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            /*
            List<Asset> assets = new List<Asset>();
            foreach (string directory in Directory.EnumerateDirectories(folderPath))
            {
                Console.WriteLine($"Directory: {directory}");
                foreach (string file in Directory.EnumerateFiles(directory))
                {
                    Console.WriteLine($"  File: {file}");
                    string content = File.ReadAllText(file);
                    Asset mountedAsset = JsonSerializer.Deserialize<Asset>(content, options);
                    assets.Add(mountedAsset);
                }
            }
            return assets;
            */
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the asset endpoint profile of the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset whose endpoint profile to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The requested asset endpoint profile.</returns>
        public async Task<AssetEndpointProfile> GetAssetEndpointProfileAsync(string assetId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            //TODO assetId is currently ignored because there is only ever one assetId deployed, currently. Will revise later once operator can deploy more than one asset per connector
            string? aepUsernameSecretName = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepUsernameSecretNameRelativeMountPath}");
            string? aepPasswordSecretName = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepPasswordSecretNameRelativeMountPath}");
            string? aepCertificateSecretName = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepCertificateSecretNameRelativeMountPath}");
            string? aepUsernameSecretFileContents = _aepUsernameSecretMountPath != null ? await GetMountedConfigurationValueAsStringAsync($"{_aepUsernameSecretMountPath}/{aepUsernameSecretName}") : null;
            byte[]? aepPasswordSecretFileContents = _aepPasswordSecretMountPath != null ? await GetMountedConfigurationValueAsync($"{_aepPasswordSecretMountPath}/{aepPasswordSecretName}") : null;
            string? aepCertFileContents = _aepCertMountPath != null ? await GetMountedConfigurationValueAsStringAsync($"{_aepCertMountPath}/{aepCertificateSecretName}"): null;

            var credentials = new AssetEndpointProfileCredentials(aepUsernameSecretFileContents, aepPasswordSecretFileContents, aepCertFileContents);

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
        /// <param name="pollingInterval">How frequently to check for changes to the asset.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task ObserveAssetAsync(string assetId, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Stop receiving notifications on <see cref="AssetFileChanged"/> when the asset with the provided Id changes.
        /// </summary>
        /// <param name="assetId">The Id of the asset to unobserve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task UnobserveAssetAsync(string assetId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Start receiving notifications on <see cref="AssetEndpointProfileFileChanged"/> when the asset endpoint profile
        /// changes for the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset whose endpoint profile you want to observe.</param>
        /// <param name="pollingInterval">How frequently to check for changes to the asset endpoint profile.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ObserveAssetEndpointProfileAsync(string assetId, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!assetEndpointProfileFileObservers.ContainsKey(assetId))
            {
                //TODO assetId is currently ignored because there is only ever one assetId deployed, currently. Will revise later once operator can deploy more than one asset per connector
                var assetEndpointObserver = new FilesObserver(
                    new(){
                        $"{_configMapMountPath}/{AepTargetAddressRelativeMountPath}",
                        $"{_configMapMountPath}/{AepAuthenticationMethodRelativeMountPath}",
                        $"{_configMapMountPath}/{EndpointProfileTypeRelativeMountPath}",
                        $"{_configMapMountPath}/{AepAdditionalConfigurationRelativeMountPath}"
                    }, 
                    pollingInterval);

                if (_aepUsernameSecretMountPath != null)
                {
                    string? aepUsernameSecretName = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepUsernameSecretNameRelativeMountPath}");
                    Debug.Assert(aepUsernameSecretName != null);
                    assetEndpointObserver.ObserveAdditionalFilePath($"{_aepUsernameSecretMountPath}/{aepUsernameSecretName}");
                }

                if (_aepPasswordSecretMountPath != null)
                {
                    string? aepPasswordSecretName = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepPasswordSecretNameRelativeMountPath}");
                    Debug.Assert(aepPasswordSecretName != null);
                    assetEndpointObserver.ObserveAdditionalFilePath($"{_aepPasswordSecretMountPath}/{aepPasswordSecretName}");
                }

                if (_aepCertMountPath != null)
                {
                    string? aepCertificateSecretName = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepCertificateSecretNameRelativeMountPath}");
                    Debug.Assert(aepCertificateSecretName != null);
                    assetEndpointObserver.ObserveAdditionalFilePath($"{_aepCertMountPath}/{aepCertificateSecretName}");
                }

                assetEndpointProfileFileObservers.Add(assetId, assetEndpointObserver);

                await assetEndpointObserver.StartAsync();

                assetEndpointObserver.OnFileChanged += OnAssetEndpointProfileFileChanged;
            }
        }

        /// <summary>
        /// Stop receiving notifications on <see cref="AssetEndpointProfileFileChanged"/> when the asset endpoint profile
        /// changes for the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset whose endpoint profile you want to unobserve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task UnobserveAssetEndpointProfileAsync(string assetId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            //TODO assetId is currently ignored because there is only ever one assetId deployed, currently. Will revise later once operator can deploy more than one asset per connector
            if (assetEndpointProfileFileObservers.ContainsKey(assetId))
            {
                await assetEndpointProfileFileObservers[assetId].StopAsync();
                assetEndpointProfileFileObservers.Remove(assetId);
            }
        }

        /// <summary>
        /// Returns the complete list of assets deployed by the operator to this pod.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The complete list of assets deployed by the operator to this pod.</returns>
        public Task<IEnumerable<string>> GetAssetIdsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        private void OnAssetEndpointProfileFileChanged(object? sender, EventArgs e)
        {
            new Task(async () =>
            {
                AssetEndpointProfileChanged?.Invoke(this, await GetAssetEndpointProfileAsync("todo"));
            }).Start();
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
