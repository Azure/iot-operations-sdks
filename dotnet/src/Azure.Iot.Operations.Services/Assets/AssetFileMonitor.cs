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
    public class AssetFileMonitor : IAssetFileMonitor
    {
        // Environment variables set by operator when connector is deployed
        internal const string AdrResourcesNameMountPathEnvVar = "ADR_RESOURCES_NAME_MOUNT_PATH";
        internal const string AepCredentialsMountPathEnvVar = "AEP_CREDENTIALS_MOUNT_PATH";

        /// </inheritdoc>
        public event EventHandler<AssetCreatedEventArgs>? AssetCreated;

        /// </inheritdoc>
        public event EventHandler<AssetDeletedEventArgs>? AssetDeleted;

        /// </inheritdoc>
        public event EventHandler<AssetEndpointProfileCreatedEventArgs>? AssetEndpointProfileCreated;

        /// </inheritdoc>
        public event EventHandler<AssetEndpointProfileDeletedEventArgs>? AssetEndpointProfileDeleted;

        public AssetFileMonitor()
        {
        }

        /// <inheritdoc/>
        public void ObserveAssets()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void UnobserveAssets()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void ObserveAssetEndpointProfile()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void UnobserveAssetEndpointProfile()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public List<string> GetAssetNames()
        {
            throw new NotImplementedException();
        }

        public List<string> GetAssetEndpointProfileNames()
        {
            throw new NotImplementedException();
        }

        private void OnAssetEndpointProfileFileChanged(object? sender, FileChangedEventArgs e)
        {
            string fileName = e.FileName;

            if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
            }
            else if (e.ChangeType == WatcherChangeTypes.Created)
            {
            }
            else if (e.ChangeType == WatcherChangeTypes.Created)
            {
            }
        }

        private void OnAssetFileCreatedOrDeleted(object? sender, FileChangedEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
            }
            else if (e.ChangeType == WatcherChangeTypes.Created)
            {
            }
            else if (e.ChangeType == WatcherChangeTypes.Created)
            {
            }
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
