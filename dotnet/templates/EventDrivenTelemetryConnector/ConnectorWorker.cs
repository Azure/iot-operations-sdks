// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public class ConnectorWorker : BackgroundService, IDisposable
    {
        private readonly ILogger<ConnectorWorker> _logger;
        private readonly TelemetryConnectorWorker _connector;

        public ConnectorWorker(ILogger<ConnectorWorker> logger, ILogger<TelemetryConnectorWorker> connectorLogger, IMqttClient mqttClient, IMessageSchemaProviderFactory datasetMessageSchemaProviderFactory, IAssetMonitor assetMonitor)
        {
            _logger = logger;
            _connector = new(connectorLogger, mqttClient, datasetMessageSchemaProviderFactory, assetMonitor);
            _connector.OnAssetAvailable += OnAssetSampleableAsync;
            _connector.OnAssetUnavailable += OnAssetNotSampleableAsync;
        }

        public void OnAssetNotSampleableAsync(object? sender, AssetUnavailabileEventArgs args)
        {
            // This callback notifies your app when an asset and its datasets can be sampled
            _logger.LogInformation("Asset with name {0} is no longer sampleable", args.AssetName);
            throw new NotImplementedException();
        }

        public void OnAssetSampleableAsync(object? sender, AssetAvailabileEventArgs args)
        {
            // This callback notifies your app when an asset and its datasets can no longer be sampled
            _logger.LogInformation("Asset with name {0} is now sampleable", args.AssetName);

            
            throw new NotImplementedException();
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await _connector.StartAsync(cancellationToken);
        }

        public override void Dispose()
        {
            base.Dispose();
            _connector.OnAssetAvailable -= OnAssetSampleableAsync;
            _connector.OnAssetUnavailable -= OnAssetNotSampleableAsync;
            _connector.Dispose();
        }
    }
}