using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Protocol.UnitTests;
using Azure.Iot.Operations.Services.Assets;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    public sealed class TelemetryConnectorWorkerTests
    {
        [Fact]
        public void ConnectorRetriesOnConnectFailures()
        {
            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAssetMonitor mockAssetMonitor = new MockAssetMonitor();
            Mock<IDatasetSamplerFactory> mockDatasetSamplerFactory = new Mock<IDatasetSamplerFactory>();
            Mock<ILogger<TelemetryConnectorWorker>> mockLogger = new Mock<ILogger<TelemetryConnectorWorker>>();
            TelemetryConnectorWorker worker = new TelemetryConnectorWorker(mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory.Object, mockAssetMonitor.Object);
            _ = worker.StartAsync(CancellationToken.None);
            var aep = new AssetEndpointProfile("localhost", "someAuthMethod", "someEndpointProfileType");
            mockAssetMonitor.AddOrUpdateMockAssetEndpointProfile(aep);
            var asset = new Asset()
            {
                Datasets =
                [
                    new Dataset()
                    {
                        Name = "someDataset",
                        DataPoints =
                        [
                            new DataPoint()
                            {
                                Name = "someDataPoint",
                            }
                        ]
                    }
                ]
            };

            mockMqttClient.OnPublishAttempt += (msg) =>
            {

                return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, "", new List<MqttUserProperty>()));
            };

            mockAssetMonitor.AddOrUpdateMockAsset("someAsset", asset);

        }
    }
}
