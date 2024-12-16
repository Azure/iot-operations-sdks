using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public static class AssetMonitorFactoryProvider
    {
        /// <summary>
        /// A provider for the default <see cref="AssetMonitor"/> implementation"/>
        /// </summary>
        public static Func<IServiceProvider, IAssetMonitor> AssetMonitorFactory = service =>
        {
            return new AssetMonitor();
        };
    }
}