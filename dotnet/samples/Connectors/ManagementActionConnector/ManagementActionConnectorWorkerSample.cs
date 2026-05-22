// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol;

namespace ManagementActionConnector
{
    /// <summary>
    /// Thin <see cref="BackgroundService"/> wrapper that owns a
    /// <see cref="ConnectorWorker"/> configured with an
    /// <see cref="IManagementActionHandlerFactory"/> and runs its
    /// <see cref="ConnectorWorker.RunConnectorAsync"/> loop. All the
    /// interesting business logic lives in the per-action handlers under
    /// <c>Handlers/</c>; the SDK base class handles executor lifecycle, notification
    /// processing, drain, and health/config reporting on our behalf.
    /// </summary>
    public sealed class ManagementActionConnectorWorkerSample(
        ApplicationContext applicationContext,
        ILogger<ManagementActionConnectorWorkerSample> logger,
        ILogger<ConnectorWorker> connectorLogger,
        IMqttClient mqttClient,
        IManagementActionHandlerFactory handlerFactory,
        IMessageSchemaProvider messageSchemaProvider,
        IAzureDeviceRegistryClientWrapperProvider adrClientFactory)
        : BackgroundService
    {
        private readonly ConnectorWorker _connector = new(
            applicationContext,
            connectorLogger,
            mqttClient,
            messageSchemaProvider,
            adrClientFactory,
            actionHandlerFactory: handlerFactory);

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting ManagementActionConnector sample worker.");
            await _connector.RunConnectorAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _connector.Dispose();
            base.Dispose();
        }
    }
}
