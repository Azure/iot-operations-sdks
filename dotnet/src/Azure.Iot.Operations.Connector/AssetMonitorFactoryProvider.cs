using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public static class AssetMonitorFactoryProvider
    {
        public static Func<IServiceProvider, IAssetMonitor> AssetMonitorFactory = service =>
        {
            return new AssetMonitor();
        };
    }
}