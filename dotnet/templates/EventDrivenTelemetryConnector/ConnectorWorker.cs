// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.Assets;

namespace EventDrivenTelemetryConnector
{
    public class ConnectorWorker : BackgroundService, IDisposable
    {
        private readonly ILogger<ConnectorWorker> _logger;
        private readonly TelemetryConnectorWorker _connector;

        /// <summary>
        /// Construct a new event-driven connector worker.
        /// </summary>
        /// <param name="applicationContext">The per session application context containing shared resources.</param>
        /// <param name="logger">The logger to use in this layer.</param>
        /// <param name="connectorLogger">The logger to use in the connector layer.</param>
        /// <param name="mqttClient">The MQTT client that the connector layer will use to connect to the broker and forward telemetry.</param>
        /// <param name="messageSchemaProviderFactory">The provider for any message schemas to associate with events forwarded as telemetry messages to the MQTT broker</param>
        /// <param name="assetMonitor">The asset monitor.</param>
        public ConnectorWorker(
            ApplicationContext applicationContext,
            ILogger<ConnectorWorker> logger,
            ILogger<TelemetryConnectorWorker> connectorLogger,
            IMqttClient mqttClient,
            IMessageSchemaProvider messageSchemaProviderFactory,
            IAssetMonitor assetMonitor)
        {
            _logger = logger;
            _connector = new(applicationContext, connectorLogger, mqttClient, messageSchemaProviderFactory, assetMonitor);
            _connector.OnAssetAvailable += OnAssetAvailableAsync;
            _connector.OnAssetUnavailable += OnAssetUnavailableAsync;
        }

        public void OnAssetAvailableAsync(object? sender, AssetAvailabileEventArgs args)
        {
            // This callback notifies your app when an asset is available and you can open a connection to your asset to start receiving events
            _logger.LogInformation("Asset with name {0} is now available", args.AssetName);

            // Once you receive an event from your asset, use the connector to forward it as telemetry to your MQTT broker
            // await _connector.ForwardReceivedEventAsync(args.Asset, args.Asset.Events[0], new byte[0]);
        }

        public void OnAssetUnavailableAsync(object? sender, AssetUnavailableEventArgs args)
        {
            // This callback notifies your app when an asset is no longer available. At this point, you should close any connection to your asset
            _logger.LogInformation("Asset with name {0} is no longer available", args.AssetName);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // This will run the connector application which connects you to the MQTT broker, optionally performs leader election, and
            // monitors for assets. As assets become available, OnAssetAvailable and OnAssetUnavailable events will execute.
            await _connector.StartAsync(cancellationToken);
        }

        public override void Dispose()
        {
            base.Dispose();
            _connector.OnAssetAvailable -= OnAssetAvailableAsync;
            _connector.OnAssetUnavailable -= OnAssetUnavailableAsync;
            _connector.Dispose();
        }
    }
}
