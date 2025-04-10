// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public static class AssetMonitorFactoryProvider
    {
        /// <summary>
        /// A provider for the default <see cref="AssetFileMonitor"/> implementation"/>
        /// </summary>
        public static Func<IServiceProvider, IAssetFileMonitor> AssetMonitorFactory = service =>
        {
            return new AssetFileMonitor();
        };
    }
}