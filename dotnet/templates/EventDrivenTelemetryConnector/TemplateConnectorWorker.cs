// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol;

namespace EventDrivenTelemetryConnector
{
    public class TemplateConnectorWorker : BackgroundService, IDisposable
    {
        private readonly ILogger<TemplateConnectorWorker> _logger;
        private readonly ConnectorWorker _connector;

        /// <summary>
        /// Construct a new event-driven connector worker.
        /// </summary>
        /// <param name="applicationContext">The per session application context containing shared resources.</param>
        /// <param name="logger">The logger to use in this layer.</param>
        /// <param name="connectorLogger">The logger to use in the connector layer.</param>
        /// <param name="mqttClient">The MQTT client that the connector layer will use to connect to the broker and forward telemetry.</param>
        /// <param name="messageSchemaProviderFactory">The provider for any message schemas to associate with events forwarded as telemetry messages to the MQTT broker</param>
        /// <param name="assetMonitor">The asset monitor.</param>
        public TemplateConnectorWorker(
            ApplicationContext applicationContext,
            ILogger<TemplateConnectorWorker> logger,
            ILogger<ConnectorWorker> connectorLogger,
            IMqttClient mqttClient,
            IMessageSchemaProvider messageSchemaProviderFactory,
            IAdrClientWrapperProvider adrClientWrapperFactory,
            IConnectorLeaderElectionConfigurationProvider? leaderElectionConfigurationProvider = null)
        {
            _logger = logger;
            _connector = new(applicationContext, connectorLogger, mqttClient, messageSchemaProviderFactory, adrClientWrapperFactory, leaderElectionConfigurationProvider);
            _connector.WhileAssetIsAvailable += WhileAssetAvailableAsync;
        }

        public Task WhileAssetAvailableAsync(AssetAvailableEventArgs e, CancellationToken cancellationToken)
        {
            // This cancellation token will signal for cancellation once the asset is no longer available.
            // It is safe to throw an OperationCancelledException from this thread.
            cancellationToken.ThrowIfCancellationRequested();

            // This callback notifies your app when an asset is available and you can open a connection to your asset to start receiving events
            _logger.LogInformation("Asset with name {0} is now available", e.AssetName);

            // Once you receive an event from your asset, use the connector to forward it as telemetry to your MQTT broker
            // await _connector.ForwardReceivedEventAsync(args.Asset, args.Asset.Events[0], new byte[0], cancellationToken);

            return Task.CompletedTask;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // This will run the connector application which connects you to the MQTT broker, optionally performs leader election, and
            // monitors for assets. As assets become available, WhileAssetAvailable events will execute for each particular asset.
            await _connector.StartAsync(cancellationToken);
        }

        public override void Dispose()
        {
            base.Dispose();
            _connector.WhileAssetIsAvailable -= WhileAssetAvailableAsync;
            _connector.Dispose();
        }
    }
}
