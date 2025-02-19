using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Services.Assets;

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

        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(AssetEndpointProfile assetEndpointProfile, Asset asset, string datasetName, Dataset dataset, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((ConnectorMessageSchema?)null);
        }

        public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(AssetEndpointProfile assetEndpointProfile, Asset asset, string eventName, Event assetEvent, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((ConnectorMessageSchema?)null);
        }
    }
}
