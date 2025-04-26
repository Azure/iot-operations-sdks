using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Connector
{
    /// <summary>
    /// An implementation of <see cref="IMessageSchemaProvider"/> where no datasets or events will register a message schema.
    /// </summary>
    public class NoMessageSchemaProvider : IMessageSchemaProvider
    {
        public static Func<IServiceProvider, IMessageSchemaProvider> NoMessageSchemaProviderFactory = service =>
        {
            return new NoMessageSchemaProvider();
        };

        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(Device device, Asset asset, string datasetName, AssetDatasetSchemaElement dataset, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((ConnectorMessageSchema?)null);
        }

        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(Device device, Asset asset, string eventName, AssetEventSchemaElement assetEvent, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((ConnectorMessageSchema?)null);
        }
    }
}
