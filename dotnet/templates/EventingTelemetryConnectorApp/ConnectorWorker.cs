// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.Assets;

namespace Azure.Iot.Operations.Connector
{
    public class ConnectorWorker : EventingTelemetryConnectorWorker
    {
        public ConnectorWorker(ILogger<EventingTelemetryConnectorWorker> logger, IMqttClient mqttClient, IDatasetSamplerFactory datasetSamplerFactory, IAssetMonitor assetMonitor) : base(logger, mqttClient, datasetSamplerFactory, assetMonitor)
        {
        }

        public override Task OnAssetNotSampleableAsync(string assetName, CancellationToken cancellationToken)
        {
            // This callback notifies your app when an asset and its datasets can be sampled
            _logger.LogInformation("Asset with name {0} is no longer sampleable", assetName);
            throw new NotImplementedException();
        }

        public override Task OnAssetSampleableAsync(string assetName, Asset asset, CancellationToken cancellationToken)
        {
            // This callback notifies your app when an asset and its datasets can no longer be sampled
            _logger.LogInformation("Asset with name {0} is now sampleable", assetName);
            throw new NotImplementedException();
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Run the base class's loop in another thread so that this thread can act independently
            _ = base.ExecuteAsync(cancellationToken);

            // Call into the base class to sample an asset's dataset. Once called, the base class will
            // sample the asset (if available), and then publish the retrieved data as telemetry to the MQTT broker.
            //base.SampleDatasetAsync(...)
        }
    }
}