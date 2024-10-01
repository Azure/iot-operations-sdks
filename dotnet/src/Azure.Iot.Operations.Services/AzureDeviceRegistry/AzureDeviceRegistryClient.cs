// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    public class AzureDeviceRegistryClient : IAsyncDisposable
    {
        // The operator will deploy the connector pod with these environment variables set.
        internal const string ConfigMapMountPathEnvVar = "CONFIGMAP_MOUNT_PATH";
        internal const string AepUsernameSecretMountPathEnvVar = "AEP_USERNAME_SECRET_MOUNT_PATH";
        internal const string AepPasswordSecretMountPathEnvVar = "AEP_PASSWORD_SECRET_MOUNT_PATH";
        internal const string AepCertMountPathEnvVar = "AEP_CERT_MOUNT_PATH";

        // The operator will deploy the connector pod with volumes with this information.
        // These particular files will be in the configmap mount path folder
        internal string AepTargetAddressRelativeMountPath = "AEP_TARGET_ADDRESS";
        internal string AepAuthenticationMethodRelativeMountPath = "AEP_AUTHENTICATION_METHOD";
        internal string AepUsernameSecretNameRelativeMountPath = "AEP_USERNAME_SECRET_NAME";
        internal string AepPasswordSecretNameRelativeMountPath = "AEP_PASSWORD_SECRET_NAME";
        internal string AepCertificateSecretNameRelativeMountPath = "AEP_CERT_SECRET_NAME";
        internal string EndpointProfileTypeRelativeMountPath = "ENDPOINT_PROFILE_TYPE";
        internal string AepAdditionalConfigurationRelativeMountPath = "AEP_ADDITIONAL_CONFIGURATION";

        private Dictionary<string, FilesObserver> assetEndpointProfileFileObservers = new();

        private string _configMapMountPath;
        private string _aepUsernameSecretMountPath;
        private string _aepPasswordSecretMountPath;
        private string _aepCertMountPath;

#pragma warning disable CS0067 // Unused for now
        public event EventHandler<Asset>? AssetChanged;
#pragma warning restore CS0067 // Unused for now
        public event EventHandler<AssetEndpointProfile>? AssetEndpointProfileChanged;

        public AzureDeviceRegistryClient()
        {
            _configMapMountPath = Environment.GetEnvironmentVariable(ConfigMapMountPathEnvVar) ?? throw new InvalidOperationException("Missing the config map mount path environment variable");
            _aepUsernameSecretMountPath = Environment.GetEnvironmentVariable(AepUsernameSecretMountPathEnvVar) ?? throw new InvalidOperationException("Missing the username secret mount path environment variable");
            _aepPasswordSecretMountPath = Environment.GetEnvironmentVariable(AepPasswordSecretMountPathEnvVar) ?? throw new InvalidOperationException("Missing the password secret mount path environment variable");
            _aepCertMountPath = Environment.GetEnvironmentVariable(AepCertMountPathEnvVar) ?? throw new InvalidOperationException("Missing the certificate secret mount path environment variable");
        }

        /// <summary>
        /// Get the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset to retrieve.</param>
        /// <returns>The requested asset.</returns>
        public Task<Asset> GetAssetAsync(string assetId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the asset endpoint profile of the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset whose endpoint profile to retrieve.</param>
        /// <returns>The requested asset endpoint profile.</returns>
        public async Task<AssetEndpointProfile> GetAssetEndpointProfileAsync(string assetId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            //TODO assetId is currently ignored because there is only ever one assetId deployed, currently. Will revise later once operator can deploy more than one asset per connector
            string? aepUsernameSecretName = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepUsernameSecretNameRelativeMountPath}");
            string? aepPasswordSecretName = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepPasswordSecretNameRelativeMountPath}");
            string? aepCertificateSecretName = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepCertificateSecretNameRelativeMountPath}");
            string? aepUsernameSecretFileContents = await GetMountedConfigurationValueAsStringAsync($"{_aepUsernameSecretMountPath}/{aepUsernameSecretName}");
            byte[]? aepPasswordSecretFileContents = await GetMountedConfigurationValueAsync($"{_aepPasswordSecretMountPath}/{aepPasswordSecretName}");
            string? aepCertFileContents = await GetMountedConfigurationValueAsStringAsync($"{_aepCertMountPath}/{aepCertificateSecretName}");

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
        public Task ObserveAssetAsync(string assetId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Stop receiving notifications on <see cref="AssetFileChanged"/> when the asset with the provided Id changes.
        /// </summary>
        /// <param name="assetId">The Id of the asset to unobserve.</param>
        public Task UnobserveAssetAsync(string assetId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Start receiving notifications on <see cref="AssetEndpointProfileFileChanged"/> when the asset endpoint profile
        /// changes for the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset whose endpoint profile you want to observe.</param>
        public async Task ObserveAssetEndpointProfileAsync(string assetId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!assetEndpointProfileFileObservers.ContainsKey(assetId))
            {
                string? aepUsernameSecretName = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepUsernameSecretNameRelativeMountPath}");
                string? aepPasswordSecretName = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepPasswordSecretNameRelativeMountPath}");
                string? aepCertificateSecretName = await GetMountedConfigurationValueAsStringAsync($"{_configMapMountPath}/{AepCertificateSecretNameRelativeMountPath}");

                //TODO assetId is currently ignored because there is only ever one assetId deployed, currently. Will revise later once operator can deploy more than one asset per connector
                var assetEndpointObserver = new FilesObserver(
                    new(){
                        $"{_configMapMountPath}/{AepTargetAddressRelativeMountPath}",
                        $"{_configMapMountPath}/{AepAuthenticationMethodRelativeMountPath}",
                        $"{_configMapMountPath}/{EndpointProfileTypeRelativeMountPath}",
                        $"{_configMapMountPath}/{AepAdditionalConfigurationRelativeMountPath}",
                        $"{_aepUsernameSecretMountPath}/{aepUsernameSecretName}",
                        $"{_aepPasswordSecretMountPath}/{aepPasswordSecretName}",
                        $"{_aepCertMountPath}/{aepCertificateSecretName}",
                    });

                assetEndpointProfileFileObservers.Add(assetId, assetEndpointObserver);

                assetEndpointObserver.Start();

                assetEndpointObserver.OnFileChanged += OnAssetEndpointProfileFileChanged;
            }
        }

        private void OnAssetEndpointProfileFileChanged(object? sender, EventArgs e)
        {
            new Task(async () =>
            {
                AssetEndpointProfileChanged?.Invoke(this, await GetAssetEndpointProfileAsync("todo"));
            }).Start();
        }

        /// <summary>
        /// Stop receiving notifications on <see cref="AssetEndpointProfileFileChanged"/> when the asset endpoint profile
        /// changes for the asset with the provided Id.
        /// </summary>
        /// <param name="assetId">The Id of the asset whose endpoint profile you want to unobserve.</param>
        public Task UnobserveAssetEndpointProfileAsync(string assetId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            //TODO assetId is currently ignored because there is only ever one assetId deployed, currently. Will revise later once operator can deploy more than one asset per connector
            if (assetEndpointProfileFileObservers.ContainsKey(assetId))
            {
                assetEndpointProfileFileObservers[assetId].Stop();
                assetEndpointProfileFileObservers.Remove(assetId);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Returns the complete list of assets deployed by the operator to this pod.
        /// </summary>
        /// <returns>The complete list of assets deployed by the operator to this pod.</returns>
        public Task<IEnumerable<string>> GetAssetIdsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Dispose this client and all its resources.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
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
