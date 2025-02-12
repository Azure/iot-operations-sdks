// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets.FileMonitor;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.Services.Assets
{
    /// <summary>
    /// This class allows for getting and monitor changes to assets and asset endpoint profiles.
    /// </summary>
    /// <remarks>
    /// This class is only applicable for connector applications that have been deployed by the Akri operator.
    /// </remarks>
    public class AssetMonitor : IAssetMonitor
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

        private FilesMonitor? _assetEndpointProfileConfigFilesObserver;
        private FilesMonitor? _assetEndpointProfileUsernameSecretFilesObserver;
        private FilesMonitor? _assetEndpointProfilePasswordSecretFilesObserver;
        private FilesMonitor? _assetEndpointProfileCertificateSecretFilesObserver;
        private FilesMonitor? _assetFilesObserver;

        /// <summary>
        /// The callback that executes when an asset has changed once you start observing an asset with
        /// <see cref="ObserveAssets(TimeSpan?, CancellationToken)"/>.
        /// </summary>
        public event EventHandler<AssetChangedEventArgs>? AssetChanged;

        /// <summary>
        /// The callback that executes when the asset endpoint profile has changed once you start observing it with
        /// <see cref="ObserveAssetEndpointProfile(TimeSpan?, CancellationToken)"/>.
        /// </summary>
        public event EventHandler<AssetEndpointProfileChangedEventArgs>? AssetEndpointProfileChanged;

        public AssetMonitor()
        {
        }

        /// <inheritdoc/>
        public async Task<Asset?> GetAssetAsync(string assetName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(GetAssetDirectory()) || !File.Exists($"{GetAssetDirectory()}/{assetName}"))
            {
                return null;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true,
            };

            byte[] assetContents = await FileUtilities.ReadFileWithRetryAsync($"{GetAssetDirectory()}/{assetName}");
            Asset asset = JsonSerializer.Deserialize<Asset>(assetContents, options) ?? throw new InvalidOperationException("Malformed asset definition. Could not deserialize into Asset type.");

            return asset;
        }

        /// <inheritdoc/>
        public async Task<AssetEndpointProfile?> GetAssetEndpointProfileAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var _aepUsernameSecretName = await GetMountedConfigurationValueAsStringAsync($"{GetAssetEndpointProfileConfigDirectory()}/{AepUsernameFileNameRelativeMountPath}");
            var _aepPasswordSecretName = await GetMountedConfigurationValueAsStringAsync($"{GetAssetEndpointProfileConfigDirectory()}/{AepPasswordFileNameRelativeMountPath}");
            var _aepCertificateSecretName = await GetMountedConfigurationValueAsStringAsync($"{GetAssetEndpointProfileConfigDirectory()}/{AepCertificateFileNameRelativeMountPath}");

            string? aepUsernameSecretFileContents = GetAepUsernameDirectory() != null ? await GetMountedConfigurationValueAsStringAsync($"{GetAepUsernameDirectory()}/{_aepUsernameSecretName}") : null;
            byte[]? aepPasswordSecretFileContents = GetAepPasswordDirectory() != null ? await GetMountedConfigurationValueAsync($"{GetAepPasswordDirectory()}/{_aepPasswordSecretName}") : null;
            string? aepCertFileContents = GetAepCertDirectory() != null ? await GetMountedConfigurationValueAsStringAsync($"{GetAepCertDirectory()}/{_aepCertificateSecretName}"): null;

            var credentials = new AssetEndpointProfileCredentials(aepUsernameSecretFileContents, aepPasswordSecretFileContents, aepCertFileContents);

            string aepTargetAddressFileContents = await GetMountedConfigurationValueAsStringAsync($"{GetAssetEndpointProfileConfigDirectory()}/{AepTargetAddressRelativeMountPath}") ?? throw new InvalidOperationException("Missing AEP target address file");
            string aepAuthenticationMethodFileContents = await GetMountedConfigurationValueAsStringAsync($"{GetAssetEndpointProfileConfigDirectory()}/{AepAuthenticationMethodRelativeMountPath}") ?? throw new InvalidOperationException("Missing AEP authentication method file");
            string endpointProfileTypeFileContents = await GetMountedConfigurationValueAsStringAsync($"{GetAssetEndpointProfileConfigDirectory()}/{EndpointProfileTypeRelativeMountPath}") ?? throw new InvalidOperationException("Missing AEP type file");
            string? aepAdditionalConfigurationFileContents = await GetMountedConfigurationValueAsStringAsync($"{GetAssetEndpointProfileConfigDirectory()}/{AepAdditionalConfigurationRelativeMountPath}");

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

        /// <inheritdoc/>
        public void ObserveAssets(TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_assetFilesObserver == null)
            {
                _assetFilesObserver = new(GetAssetDirectory, pollingInterval);
                _assetFilesObserver.OnFileChanged += OnAssetFileChanged;
                _assetFilesObserver.Start();
            }
        }

        /// <inheritdoc/>
        public void UnobserveAssets(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_assetFilesObserver != null)
            {
                _assetFilesObserver.Stop();
                _assetFilesObserver.OnFileChanged -= OnAssetFileChanged;
                _assetFilesObserver = null;
            }
        }

        /// <inheritdoc/>
        public void ObserveAssetEndpointProfile(TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_assetEndpointProfileConfigFilesObserver == null)
            {
                // Asset endpoint profile files live in a few different directories, so several file directory observers
                // are needed
                _assetEndpointProfileConfigFilesObserver = new(GetAssetEndpointProfileConfigDirectory, pollingInterval);
                _assetEndpointProfileConfigFilesObserver.OnFileChanged += OnAssetEndpointProfileFileChanged;
                _assetEndpointProfileConfigFilesObserver.Start();

                _assetEndpointProfileUsernameSecretFilesObserver = new(GetAepUsernameDirectory, pollingInterval);
                _assetEndpointProfileUsernameSecretFilesObserver.OnFileChanged += OnAssetEndpointProfileFileChanged;
                _assetEndpointProfileUsernameSecretFilesObserver.Start();

                _assetEndpointProfilePasswordSecretFilesObserver = new(GetAepPasswordDirectory, pollingInterval);
                _assetEndpointProfilePasswordSecretFilesObserver.OnFileChanged += OnAssetEndpointProfileFileChanged;
                _assetEndpointProfilePasswordSecretFilesObserver.Start();

                _assetEndpointProfileCertificateSecretFilesObserver = new(GetAepCertDirectory, pollingInterval);
                _assetEndpointProfileCertificateSecretFilesObserver.OnFileChanged += OnAssetEndpointProfileFileChanged;
                _assetEndpointProfileCertificateSecretFilesObserver.Start();
            }
        }

        /// <inheritdoc/>
        public void UnobserveAssetEndpointProfile(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_assetEndpointProfileConfigFilesObserver != null)
            {
                _assetEndpointProfileConfigFilesObserver.Start();
                _assetEndpointProfileConfigFilesObserver.OnFileChanged -= OnAssetFileChanged;
                _assetEndpointProfileConfigFilesObserver = null;
            }

            if (_assetEndpointProfileUsernameSecretFilesObserver != null)
            {
                _assetEndpointProfileUsernameSecretFilesObserver!.Start();
                _assetEndpointProfileUsernameSecretFilesObserver.OnFileChanged -= OnAssetFileChanged;
                _assetEndpointProfileUsernameSecretFilesObserver = null;
            }

            if (_assetEndpointProfilePasswordSecretFilesObserver != null)
            {
                _assetEndpointProfilePasswordSecretFilesObserver!.Start();
                _assetEndpointProfilePasswordSecretFilesObserver.OnFileChanged -= OnAssetFileChanged;
                _assetEndpointProfilePasswordSecretFilesObserver = null;
            }

            if (_assetEndpointProfileCertificateSecretFilesObserver != null)
            {
                _assetEndpointProfileCertificateSecretFilesObserver!.Stop();
                _assetEndpointProfileCertificateSecretFilesObserver.OnFileChanged -= OnAssetFileChanged;
                _assetEndpointProfileCertificateSecretFilesObserver = null;
            }
        }

        /// <inheritdoc/>
        public Task<List<string>> GetAssetNamesAsync(CancellationToken cancellationToken = default)
        {
            List<string> assetNames = new();
            string directoryPath = GetAssetDirectory();
            if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
            {
                foreach (string fileName in Directory.EnumerateFiles(directoryPath))
                {
                    assetNames.Add(Path.GetFileName(fileName));
                }
            }

            return Task.FromResult(assetNames);
        }

        private void OnAssetEndpointProfileFileChanged(object? sender, FileChangedEventArgs e)
        {
            string fileName = e.FileName;

            _ = Task.Run(async () =>
            {
                if (e.ChangeType == ChangeType.Deleted)
                {
                    // Don't bother trying to fetch the aep since it won't exist
                    AssetEndpointProfileChanged?.Invoke(this, new(e.ChangeType, null));
                }
                else
                {
                    AssetEndpointProfileChanged?.Invoke(this, new(e.ChangeType, await GetAssetEndpointProfileAsync()));
                }
            });
        }

        private void OnAssetFileChanged(object? sender, FileChangedEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                if (e.ChangeType == ChangeType.Deleted)
                {
                    AssetChanged?.Invoke(this, new(e.FileName, e.ChangeType, null));
                }
                else
                {
                    AssetChanged?.Invoke(this, new(e.FileName, e.ChangeType, await GetAssetAsync(e.FileName)));
                }
            });
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

        private string GetAssetDirectory()
        {
            return Environment.GetEnvironmentVariable(AssetConfigMapMountPathEnvVar) ?? "";
        }

        private string GetAssetEndpointProfileConfigDirectory()
        {
            return Environment.GetEnvironmentVariable(AssetEndpointProfileConfigMapMountPathEnvVar) ?? throw new InvalidOperationException("Missing the AEP config map mount path environment variable");
        }

        private string GetAepUsernameDirectory()
        {
            return Environment.GetEnvironmentVariable(AepUsernameSecretMountPathEnvVar) ?? "";
        }

        private string GetAepPasswordDirectory()
        {
            return Environment.GetEnvironmentVariable(AepPasswordSecretMountPathEnvVar) ?? "";
        }

        private string GetAepCertDirectory()
        {
            return Environment.GetEnvironmentVariable(AepCertMountPathEnvVar) ?? "";
        }
    }
}
