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
        internal const string AssetEndpointProfileConfigMapMountPathEnvVar = "AEP_CONFIGMAP_MOUNT_PATH";
        internal const string AssetConfigMapMountPathEnvVar = "ASSET_CONFIGMAP_MOUNT_PATH";
        internal const string AepUsernameSecretMountPathEnvVar = "AEP_USERNAME_SECRET_MOUNT_PATH";
        internal const string AepPasswordSecretMountPathEnvVar = "AEP_PASSWORD_SECRET_MOUNT_PATH";
        internal const string AepCertMountPathEnvVar = "AEP_CERT_MOUNT_PATH";

        // The operator will deploy the connector pod with volumes with this information.
        // These particular files will be in the configmap mount path folder
        internal const string AepTargetAddressRelativeMountPath = "AEP_TARGET_ADDRESS";
        internal const string AepAuthenticationMethodRelativeMountPath = "AEP_AUTHENTICATION_METHOD";
        internal const string AepUsernameFileNameRelativeMountPath = "AEP_USERNAME_FILE_NAME";
        internal const string AepPasswordFileNameRelativeMountPath = "AEP_PASSWORD_FILE_NAME";
        internal const string AepCertificateFileNameRelativeMountPath = "AEP_CERT_FILE_NAME";
        internal const string EndpointProfileTypeRelativeMountPath = "ENDPOINT_PROFILE_TYPE";
        internal const string AepAdditionalConfigurationRelativeMountPath = "AEP_ADDITIONAL_CONFIGURATION";
        internal const string AepDiscoveredAssetEndpointProfileRefRelativeMountPath = "AEP_DISCOVERED_ASSET_ENDPOINT_PROFILE_REF";
        internal const string AepUuidRelativeMountPath = "AEP_UUID";

        private FilesObserver? _assetEndpointProfileFileObserver;
        private FilesObserver? _assetFilesObserver;

        private string? _assetConfigMapMountPath;
        private string _assetEndpointConfigMapMountPath;
        private string? _aepUsernameSecretMountPath;
        private string? _aepPasswordSecretMountPath;
        private string? _aepCertMountPath;

        /// <summary>
        /// The callback that executes when an asset has changed once you start observing an asset with 
        /// <see cref="ObserveAssetAsync(string, TimeSpan?, CancellationToken)"/>.
        /// </summary>
        public event EventHandler<AssetChangedEventArgs>? AssetChanged;

        /// <summary>
        /// The callback that executes when the asset endpoint profile has changed once you start observing it with
        /// <see cref="ObserveAssetEndpointProfileAsync(TimeSpan?, CancellationToken)"/>.
        /// </summary>
        public event EventHandler<AssetEndpointProfileChangedEventArgs>? AssetEndpointProfileChanged;

        public AzureDeviceRegistryClient()
        {
            //TODO safe to assume at least one asset and one asset endpoint?
            _assetConfigMapMountPath = Environment.GetEnvironmentVariable(AssetConfigMapMountPathEnvVar);
            _assetEndpointConfigMapMountPath = Environment.GetEnvironmentVariable(AssetEndpointProfileConfigMapMountPathEnvVar) ?? throw new InvalidOperationException("Missing the AEP config map mount path environment variable");
            _aepUsernameSecretMountPath = Environment.GetEnvironmentVariable(AepUsernameSecretMountPathEnvVar);
            _aepPasswordSecretMountPath = Environment.GetEnvironmentVariable(AepPasswordSecretMountPathEnvVar);
            _aepCertMountPath = Environment.GetEnvironmentVariable(AepCertMountPathEnvVar);
        }

        /// <summary>
        /// Get the asset with the provided Id.
        /// </summary>
        /// <param name="assetName">The Id of the asset to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The requested asset.</returns>
        public async Task<Asset?> GetAssetAsync(string assetName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_assetConfigMapMountPath))
            {
                return null;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true,
            };

            byte[] assetContents = await FileUtilities.ReadFileWithRetryAsync($"{_assetConfigMapMountPath}/{assetName}");
            Asset asset = JsonSerializer.Deserialize<Asset>(assetContents, options) ?? throw new InvalidOperationException("TODO when is this possible?");

            return asset;
        }

        /// <summary>
        /// Get the asset endpoint profile of the asset with the provided Id.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The requested asset endpoint profile.</returns>
        public async Task<AssetEndpointProfile> GetAssetEndpointProfileAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? aepUsernameSecretName = await GetMountedConfigurationValueAsStringAsync($"{_assetEndpointConfigMapMountPath}/{AepUsernameFileNameRelativeMountPath}");
            string? aepPasswordSecretName = await GetMountedConfigurationValueAsStringAsync($"{_assetEndpointConfigMapMountPath}/{AepPasswordFileNameRelativeMountPath}");
            string? aepCertificateSecretName = await GetMountedConfigurationValueAsStringAsync($"{_assetEndpointConfigMapMountPath}/{AepCertificateFileNameRelativeMountPath}");
            string? aepUsernameSecretFileContents = _aepUsernameSecretMountPath != null ? await GetMountedConfigurationValueAsStringAsync($"{_aepUsernameSecretMountPath}/{aepUsernameSecretName}") : null;
            byte[]? aepPasswordSecretFileContents = _aepPasswordSecretMountPath != null ? await GetMountedConfigurationValueAsync($"{_aepPasswordSecretMountPath}/{aepPasswordSecretName}") : null;
            string? aepCertFileContents = _aepCertMountPath != null ? await GetMountedConfigurationValueAsStringAsync($"{_aepCertMountPath}/{aepCertificateSecretName}"): null;

            var credentials = new AssetEndpointProfileCredentials(aepUsernameSecretFileContents, aepPasswordSecretFileContents, aepCertFileContents);

            string aepTargetAddressFileContents = await GetMountedConfigurationValueAsStringAsync($"{_assetEndpointConfigMapMountPath}/{AepTargetAddressRelativeMountPath}") ?? throw new InvalidOperationException("TODO");
            string aepAuthenticationMethodFileContents = await GetMountedConfigurationValueAsStringAsync($"{_assetEndpointConfigMapMountPath}/{AepAuthenticationMethodRelativeMountPath}") ?? throw new InvalidOperationException("TODO");
            string endpointProfileTypeFileContents = await GetMountedConfigurationValueAsStringAsync($"{_assetEndpointConfigMapMountPath}/{EndpointProfileTypeRelativeMountPath}") ?? throw new InvalidOperationException("TODO");
            string? aepAdditionalConfigurationFileContents = await GetMountedConfigurationValueAsStringAsync($"{_assetEndpointConfigMapMountPath}/{AepAdditionalConfigurationRelativeMountPath}");

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
        /// Start receiving notifications on <see cref="AssetFileChanged"/> when any asset changes.
        /// </summary>
        /// <param name="pollingInterval">How frequently to check for changes to the asset.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ObserveAssetsAsync(TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_assetFilesObserver == null)
            {
                _assetFilesObserver = new($"{_assetConfigMapMountPath}");
                _assetFilesObserver.OnFileChanged += OnAssetFileChanged;
                await _assetFilesObserver.StartAsync();
            }
        }

        /// <summary>
        /// Stop receiving notifications on <see cref="AssetFileChanged"/> when an asset changes.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task UnobserveAssetsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_assetFilesObserver != null)
            {
                await _assetFilesObserver.StopAsync();
                _assetFilesObserver.OnFileChanged -= OnAssetFileChanged;
                _assetFilesObserver = null;
            }
        }

        /// <summary>
        /// Start receiving notifications on <see cref="AssetEndpointProfileFileChanged"/> when the asset endpoint profile
        /// changes for the asset with the provided Id.
        /// </summary>
        /// <param name="pollingInterval">How frequently to check for changes to the asset endpoint profile.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ObserveAssetEndpointProfileAsync(TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_assetEndpointProfileFileObserver == null)
            {
                _assetEndpointProfileFileObserver = new($"{_assetEndpointConfigMapMountPath}");
                _assetEndpointProfileFileObserver.OnFileChanged += OnAssetEndpointProfileFileChanged;
                await _assetEndpointProfileFileObserver.StartAsync();
            }
        }

        /// <summary>
        /// Stop receiving notifications on <see cref="AssetEndpointProfileFileChanged"/> when the asset endpoint profile
        /// changes for the asset with the provided Id.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task UnobserveAssetEndpointProfileAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_assetEndpointProfileFileObserver != null)
            {
                await _assetEndpointProfileFileObserver.StopAsync();
                _assetEndpointProfileFileObserver.OnFileChanged -= OnAssetFileChanged;
                _assetEndpointProfileFileObserver = null;
            }
        }

        /// <summary>
        /// Returns the complete list of assets deployed by the operator to this pod.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The complete list of assets deployed by the operator to this pod.</returns>
        public Task<List<string>> GetAssetNamesAsync(CancellationToken cancellationToken = default)
        {
            List<string> assetNames = new();
            if (Directory.Exists(_assetConfigMapMountPath))
            {
                foreach (string directory in Directory.EnumerateDirectories(_assetConfigMapMountPath))
                {
                    foreach (string fileName in Directory.EnumerateFiles(directory))
                    {
                        assetNames.Add(Path.GetFileName(fileName));
                    }
                }
            }

            return Task.FromResult(assetNames);
        }

        private void OnAssetEndpointProfileFileChanged(object? sender, FileChangedEventArgs e)
        {
            if (!e.FilePath.Equals($"{_assetEndpointConfigMapMountPath}/{AepTargetAddressRelativeMountPath}")
                && !e.FilePath.Equals($"{_assetEndpointConfigMapMountPath}/{AepAuthenticationMethodRelativeMountPath}")
                && !e.FilePath.Equals($"{_assetEndpointConfigMapMountPath}/{EndpointProfileTypeRelativeMountPath}")
                && !e.FilePath.Equals($"{_assetEndpointConfigMapMountPath}/{AepAdditionalConfigurationRelativeMountPath}")
                && !e.FilePath.Equals($"{_assetEndpointConfigMapMountPath}/{AepUsernameFileNameRelativeMountPath}")
                && !e.FilePath.Equals($"{_assetEndpointConfigMapMountPath}/{AepPasswordFileNameRelativeMountPath}")
                && !e.FilePath.Equals($"{_assetEndpointConfigMapMountPath}/{AepCertificateFileNameRelativeMountPath}"))
            {
                // The file that changed in the AEP config map mount directory wasn't one of the AEP files, so it can be ignored
                return;
            }

            new Task(async () =>
            {
                AssetEndpointProfileChanged?.Invoke(this, new(e.ChangeType, await GetAssetEndpointProfileAsync()));
            }).Start();
        }

        private void OnAssetFileChanged(object? sender, FileChangedEventArgs e)
        {
            string assetName = (string)sender!;
            new Task(async () =>
            {
                AssetChanged?.Invoke(this, new(assetName, e.ChangeType, await GetAssetAsync(assetName)));
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
