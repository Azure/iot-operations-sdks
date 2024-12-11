using Azure.Iot.Operations.Protocol;
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
            Mock<IMqttClient> mockMqttClient = new Mock<IMqttClient>();
            MockAssetMonitor mockAssetMonitor = new MockAssetMonitor();
            Mock<IDatasetSamplerFactory> mockDatasetSamplerFactory = new Mock<IDatasetSamplerFactory>();
            Mock<ILogger<TelemetryConnectorWorker>> mockLogger = new Mock<ILogger<TelemetryConnectorWorker>>();
            TelemetryConnectorWorker worker = new TelemetryConnectorWorker(mockLogger.Object, mockMqttClient.Object, mockDatasetSamplerFactory.Object, mockAssetMonitor.Object);
            _ = worker.StartAsync(CancellationToken.None);
            mockAssetMonitor.AddOrUpdateMockAssetEndpointProfile
        }
    }
}
