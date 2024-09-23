using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    public class AzureDeviceRegistryClient : IDisposable
    {
        // The operator will deploy the connector pod with these environment variables set.
        string ConfigMapMountPathEnvVar = "CONFIGMAP_MOUNT_PATH";
        string AepUsernameSecretMountPathEnvVar = "AEP_USERNAME_SECRET_MOUNT_PATH";
        string AepPasswordSecretMountPathEnvVar = "AEP_PASSWORD_SECRET_MOUNT_PATH";
        string AepCertMountPathEnvVar = "AEP_CERT_MOUNT_PATH";

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


        FileSystemWatcher credentialsFilesSystemWatcher = new();
        FileSystemWatcher assetFilesSystemWatcher = new();

        public event EventHandler<AssetEndpointProfileCredentials>? CredentialsFileChanged;
        public event EventHandler<AssetEndpointProfile>? AssetFileChanged;

        public AzureDeviceRegistryClient()
        {
            _configMapMountPath = Environment.GetEnvironmentVariable(ConfigMapMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            _aepUsernameSecretMountPath = Environment.GetEnvironmentVariable(AepUsernameSecretMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            _aepPasswordSecretMountPath = Environment.GetEnvironmentVariable(AepPasswordSecretMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
            _aepCertMountPath = Environment.GetEnvironmentVariable(AepCertMountPathEnvVar) ?? throw new ArgumentException("TODO misconfiguration");
        }

        //TODO what all constitutes the asset here? Additional config + auth method + endpoint profile + target address?
        public AssetEndpointProfile GetAsset()
        {
            string aepTargetAddressFileContents = GetMountedConfigurationValue($"{_configMapMountPath}/{AepTargetAddressRelativeMountPath}") ?? throw new InvalidOperationException("TODO");
            string aepAuthenticationMethodFileContents = GetMountedConfigurationValue($"{_configMapMountPath}/{AepAuthenticationMethodRelativeMountPath}") ?? throw new InvalidOperationException("TODO");
            string endpointProfileTypeFileContents = GetMountedConfigurationValue($"{_configMapMountPath}/{EndpointProfileTypeRelativeMountPath}") ?? throw new InvalidOperationException("TODO");
            string? aepAdditionalConfigurationFileContents = GetMountedConfigurationValue($"{_configMapMountPath}/{AepAdditionalConfigurationRelativeMountPath}");

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

            return new AssetEndpointProfile(aepTargetAddressFileContents, aepAuthenticationMethodFileContents, endpointProfileTypeFileContents, aepAdditionalConfigurationJson);
        }

        public AssetEndpointProfileCredentials GetAssetCredentials()
        {
            string? aepUsernameSecretName = GetMountedConfigurationValue($"{_configMapMountPath}/{AepUsernameSecretNameRelativeMountPath}");
            string? aepPasswordSecretName = GetMountedConfigurationValue($"{_configMapMountPath}/{AepPasswordSecretNameRelativeMountPath}");
            string? aepUsernameSecretFileContents = GetMountedConfigurationValue($"{_aepUsernameSecretMountPath}/{aepUsernameSecretName}");
            string? aepPasswordSecretFileContents = GetMountedConfigurationValue($"{_aepPasswordSecretMountPath}/{aepPasswordSecretName}");
            string? aepCertFileContents = GetMountedConfigurationValue(_aepCertMountPath);

            X509Certificate2? aepCert = null;
            if (aepCertFileContents != null)
            {
                //TODO this is a PEM file, right?
                aepCert = X509Certificate2.CreateFromPemFile(aepCertFileContents);
            }

            return new AssetEndpointProfileCredentials(aepUsernameSecretFileContents, aepPasswordSecretFileContents, aepCert);
        }

        public void ObserveAsset()
        {
            if (!assetFilesSystemWatcher.Filters.Contains($"{_configMapMountPath}/{AepTargetAddressRelativeMountPath}"))
            {
                assetFilesSystemWatcher.Filters.Add($"{_configMapMountPath}/{AepTargetAddressRelativeMountPath}");
                assetFilesSystemWatcher.Filters.Add($"{_configMapMountPath}/{AepAuthenticationMethodRelativeMountPath}");
                assetFilesSystemWatcher.Filters.Add($"{_configMapMountPath}/{EndpointProfileTypeRelativeMountPath}");
                assetFilesSystemWatcher.Filters.Add($"{_configMapMountPath}/{AepAdditionalConfigurationRelativeMountPath}");

                assetFilesSystemWatcher.Changed += OnAssetFileChanged;

                assetFilesSystemWatcher.EnableRaisingEvents = true;
            }
        }

        public void UnobserveAsset()
        {
            if (assetFilesSystemWatcher.Filters.Contains($"{_configMapMountPath}/{AepTargetAddressRelativeMountPath}"))
            {
                assetFilesSystemWatcher.Filters.Remove($"{_configMapMountPath}/{AepTargetAddressRelativeMountPath}");
                assetFilesSystemWatcher.Filters.Remove($"{_configMapMountPath}/{AepAuthenticationMethodRelativeMountPath}");
                assetFilesSystemWatcher.Filters.Remove($"{_configMapMountPath}/{EndpointProfileTypeRelativeMountPath}");
                assetFilesSystemWatcher.Filters.Remove($"{_configMapMountPath}/{AepAdditionalConfigurationRelativeMountPath}");

                assetFilesSystemWatcher.Changed -= OnAssetFileChanged;

                assetFilesSystemWatcher.EnableRaisingEvents = false;
            }
        }

        public void ObserveAssetCredentials()
        {
            if (!credentialsFilesSystemWatcher.Filters.Contains(_aepUsernameSecretMountPath))
            {
                credentialsFilesSystemWatcher.Filters.Add(_aepUsernameSecretMountPath);
                credentialsFilesSystemWatcher.Filters.Add(_aepPasswordSecretMountPath);
                credentialsFilesSystemWatcher.Filters.Add(_aepCertMountPath);

                credentialsFilesSystemWatcher.Changed += OnCredentialsFileChanged;

                credentialsFilesSystemWatcher.EnableRaisingEvents = true;
            }
        }

        public void UnobserveAssetCredentials()
        {
            if (credentialsFilesSystemWatcher.Filters.Contains(_aepUsernameSecretMountPath))
            {
                credentialsFilesSystemWatcher.Filters.Remove(_aepUsernameSecretMountPath);
                credentialsFilesSystemWatcher.Filters.Remove(_aepPasswordSecretMountPath);
                credentialsFilesSystemWatcher.Filters.Remove(_aepCertMountPath);

                credentialsFilesSystemWatcher.Changed -= OnCredentialsFileChanged;

                credentialsFilesSystemWatcher.EnableRaisingEvents = false;
            }
        }

        private void OnCredentialsFileChanged(object sender, FileSystemEventArgs e)
        {
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Changed:
                    CredentialsFileChanged?.Invoke(this, GetAssetCredentials());
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
        }

        private void OnAssetFileChanged(object sender, FileSystemEventArgs e)
        {
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Changed:
                    AssetFileChanged?.Invoke(this, GetAsset());
                    break;
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Renamed:
                case WatcherChangeTypes.Deleted:
                default:
                    // This would only happen if the user is messing around with these files for some reason. Under
                    // normal conditions, the credentials files should only ever be updated in place with new credentials
                    Trace.TraceWarning("One or more asset files was renamed/deleted/created unexpectedly");
                    break;
            }
        }

        public void Dispose()
        {
            credentialsFilesSystemWatcher.Dispose();
            assetFilesSystemWatcher.Dispose();
        }

        private static string? GetMountedConfigurationValue(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using (var reader = new StreamReader(path))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
