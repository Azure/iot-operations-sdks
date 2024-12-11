using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.Assets;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    public sealed class TelemetryConnectorWorkerTests
    {
        [Fact]
        public async Task HappyPath()
        {
            Environment.SetEnvironmentVariable("AEP_CONFIGMAP_MOUNT_PATH", "./TestMountFiles");

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAssetMonitor mockAssetMonitor = new MockAssetMonitor();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            Mock<ILogger<TelemetryConnectorWorker>> mockLogger = new Mock<ILogger<TelemetryConnectorWorker>>();
            TelemetryConnectorWorker worker = new TelemetryConnectorWorker(mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, mockAssetMonitor);
            _ = worker.StartAsync(CancellationToken.None);
            var aep = new AssetEndpointProfile("localhost", "someAuthMethod", "someEndpointProfileType");
            mockAssetMonitor.AddOrUpdateMockAssetEndpointProfile(aep);
            string expectedMqttTopic = "some/asset/telemetry/topic";
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
                        ],
                        Topic = new()
                        {
                            Path = expectedMqttTopic,
                        },
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 4000}")
                    }
                ]
            };

            TaskCompletionSource assetTelemetryForwardedToBrokerTcs = new();
            mockMqttClient.OnPublishAttempt += (msg) =>
            {
                if (string.Equals(msg.Topic, expectedMqttTopic))
                {
                    assetTelemetryForwardedToBrokerTcs.TrySetResult();
                }
                return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, "", new List<MqttUserProperty>()));
            };

            mockAssetMonitor.AddOrUpdateMockAsset("someAsset", asset);

            await assetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}
