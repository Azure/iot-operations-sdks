﻿// Copyright (c) Microsoft Corporation.
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

        private FilesObserver? _assetEndpointProfileConfigFilesObserver;
        private FilesObserver? _assetEndpointProfileUsernameSecretFilesObserver;
        private FilesObserver? _assetEndpointProfilePasswordSecretFilesObserver;
        private FilesObserver? _assetEndpointProfileCertificateSecretFilesObserver;
        private FilesObserver? _assetFilesObserver;

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
            //TODO safe to assume at least one asset endpoint? Assets are optional apparently
        }

        /// <summary>
        /// Get the asset with the provided Id.
        /// </summary>
        /// <param name="assetName">The Id of the asset to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The requested asset.</returns>
        public async Task<Asset?> GetAssetAsync(string assetName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(GetAssetDirectory()))
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

            var _aepUsernameSecretName = await GetMountedConfigurationValueAsStringAsync($"{GetAssetEndpointProfileConfigDirectory()}/{AepUsernameFileNameRelativeMountPath}");
            var _aepPasswordSecretName = await GetMountedConfigurationValueAsStringAsync($"{GetAssetEndpointProfileConfigDirectory()}/{AepPasswordFileNameRelativeMountPath}");
            var _aepCertificateSecretName = await GetMountedConfigurationValueAsStringAsync($"{GetAssetEndpointProfileConfigDirectory()}/{AepCertificateFileNameRelativeMountPath}");
            
            string? aepUsernameSecretFileContents = GetAepUsernameDirectory() != null ? await GetMountedConfigurationValueAsStringAsync($"{GetAepUsernameDirectory()}/{_aepUsernameSecretName}") : null;
            byte[]? aepPasswordSecretFileContents = GetAepPasswordDirectory() != null ? await GetMountedConfigurationValueAsync($"{GetAepPasswordDirectory()}/{_aepPasswordSecretName}") : null;
            string? aepCertFileContents = GetAepCertDirectory() != null ? await GetMountedConfigurationValueAsStringAsync($"{GetAepCertDirectory()}/{_aepCertificateSecretName}"): null;

            var credentials = new AssetEndpointProfileCredentials(aepUsernameSecretFileContents, aepPasswordSecretFileContents, aepCertFileContents);

            string aepTargetAddressFileContents = await GetMountedConfigurationValueAsStringAsync($"{GetAssetEndpointProfileConfigDirectory()}/{AepTargetAddressRelativeMountPath}") ?? throw new InvalidOperationException("TODO");
            string aepAuthenticationMethodFileContents = await GetMountedConfigurationValueAsStringAsync($"{GetAssetEndpointProfileConfigDirectory()}/{AepAuthenticationMethodRelativeMountPath}") ?? throw new InvalidOperationException("TODO");
            string endpointProfileTypeFileContents = await GetMountedConfigurationValueAsStringAsync($"{GetAssetEndpointProfileConfigDirectory()}/{EndpointProfileTypeRelativeMountPath}") ?? throw new InvalidOperationException("TODO");
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
                _assetFilesObserver = new(GetAssetDirectory, pollingInterval);
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

            if (_assetEndpointProfileConfigFilesObserver == null)
            {
                // Asset endpoint profile files live in a few different directories, so several file directory observers
                // are needed
                _assetEndpointProfileConfigFilesObserver = new(GetAssetEndpointProfileConfigDirectory, pollingInterval);
                _assetEndpointProfileConfigFilesObserver.OnFileChanged += OnAssetEndpointProfileFileChanged;
                await _assetEndpointProfileConfigFilesObserver.StartAsync();

                _assetEndpointProfileUsernameSecretFilesObserver = new(GetAepUsernameDirectory, pollingInterval);
                _assetEndpointProfileUsernameSecretFilesObserver.OnFileChanged += OnAssetEndpointProfileFileChanged;
                await _assetEndpointProfileUsernameSecretFilesObserver.StartAsync();

                _assetEndpointProfilePasswordSecretFilesObserver = new(GetAepPasswordDirectory, pollingInterval);
                _assetEndpointProfilePasswordSecretFilesObserver.OnFileChanged += OnAssetEndpointProfileFileChanged;
                await _assetEndpointProfilePasswordSecretFilesObserver.StartAsync();

                _assetEndpointProfileCertificateSecretFilesObserver = new(GetAepCertDirectory, pollingInterval);
                _assetEndpointProfileCertificateSecretFilesObserver.OnFileChanged += OnAssetEndpointProfileFileChanged;
                await _assetEndpointProfileCertificateSecretFilesObserver.StartAsync();
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

            if (_assetEndpointProfileConfigFilesObserver != null)
            {
                await _assetEndpointProfileConfigFilesObserver.StopAsync();
                _assetEndpointProfileConfigFilesObserver.OnFileChanged -= OnAssetFileChanged;
                _assetEndpointProfileConfigFilesObserver = null;
            }

            if (_assetEndpointProfileUsernameSecretFilesObserver != null)
            {
                await _assetEndpointProfileUsernameSecretFilesObserver!.StopAsync();
                _assetEndpointProfileUsernameSecretFilesObserver.OnFileChanged -= OnAssetFileChanged;
                _assetEndpointProfileUsernameSecretFilesObserver = null;
            }

            if (_assetEndpointProfilePasswordSecretFilesObserver != null)
            {
                await _assetEndpointProfilePasswordSecretFilesObserver!.StopAsync();
                _assetEndpointProfilePasswordSecretFilesObserver.OnFileChanged -= OnAssetFileChanged;
                _assetEndpointProfilePasswordSecretFilesObserver = null;
            }

            if (_assetEndpointProfileCertificateSecretFilesObserver != null)
            {
                await _assetEndpointProfileCertificateSecretFilesObserver!.StopAsync();
                _assetEndpointProfileCertificateSecretFilesObserver.OnFileChanged -= OnAssetFileChanged;
                _assetEndpointProfileCertificateSecretFilesObserver = null;
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

            //TODO do we care about filtering out changes to unrelated files?

            new Task(async () =>
            {
                //TODO an AEP can only be updated, right? Some files that hold information about the AEP
                // may be created/deleted over time (password file may disappear if credentials no longer needed)
                // but it may be a stretch to mark all file changes as "updates"
                AssetEndpointProfileChanged?.Invoke(this, new(ChangeType.Updated, await GetAssetEndpointProfileAsync()));
            }).Start();
        }

        private void OnAssetFileChanged(object? sender, FileChangedEventArgs e)
        {
            new Task(async () =>
            {
                AssetChanged?.Invoke(this, new(e.FileName, e.ChangeType, await GetAssetAsync(e.FileName)));
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
