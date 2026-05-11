// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Protocol;

namespace ManagementActionConnector
{
    /// <summary>
    /// Thin <see cref="BackgroundService"/> wrapper that owns a
    /// <see cref="Azure.Iot.Operations.Connector.ManagementActionConnectorWorker"/>
    /// and runs its <see cref="ConnectorWorker.RunConnectorAsync"/> loop. All the
    /// interesting business logic lives in the per-action handlers under
    /// <c>Handlers/</c>; the SDK base class handles executor lifecycle, notification
    /// processing, drain, and health/config reporting on our behalf.
    /// </summary>
    public sealed class ManagementActionConnectorWorkerSample : BackgroundService
    {
        private readonly ILogger<ManagementActionConnectorWorkerSample> _logger;
        private readonly Azure.Iot.Operations.Connector.ManagementActionConnectorWorker _connector;

        public ManagementActionConnectorWorkerSample(
            ApplicationContext applicationContext,
            ILogger<ManagementActionConnectorWorkerSample> logger,
            ILogger<ConnectorWorker> connectorLogger,
            IMqttClient mqttClient,
            IManagementActionHandlerFactory handlerFactory,
            IMessageSchemaProvider messageSchemaProvider,
            IAzureDeviceRegistryClientWrapperProvider adrClientFactory)
        {
            _logger = logger;
            _connector = new Azure.Iot.Operations.Connector.ManagementActionConnectorWorker(
                applicationContext,
                connectorLogger,
                mqttClient,
                handlerFactory,
                messageSchemaProvider,
                adrClientFactory);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting ManagementActionConnector sample worker.");
            await _connector.RunConnectorAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _connector.Dispose();
            base.Dispose();
        }
    }
}
