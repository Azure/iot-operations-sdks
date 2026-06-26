// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.ConnectorConfigurations;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    // These tests rely on environment variables which may interfere with other similar tests
    [Collection("Environment Variable Sequential")]
    public sealed class ConnectorWorkerAssetRaceTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

        public ConnectorWorkerAssetRaceTests()
        {
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.ConnectorConfigMountPathEnvVar, "./connector-config-no-auth-no-tls");
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.ConnectorClientIdEnvVar, "someClientId");
        }

        private sealed class MockAdrClientFactory : IAzureDeviceRegistryClientWrapperProvider
        {
            private readonly IAzureDeviceRegistryClientWrapper _mockAdrClientWrapper;

            public MockAdrClientFactory(IAzureDeviceRegistryClientWrapper mockAdrClientWrapper)
            {
                _mockAdrClientWrapper = mockAdrClientWrapper;
            }

            public IAzureDeviceRegistryClientWrapper CreateAdrClientWrapper(ApplicationContext applicationContext, IMqttPubSubClient mqttPubSubClient)
            {
                return _mockAdrClientWrapper;
            }
        }

        /// <summary>
        /// Regression test for the device/asset churn race. A connector reports its own
        /// device/endpoint status, and each such write makes ADR emit a device <c>Updated</c>
        /// notification. The old handler tore the device down (removing it from the tracked set and
        /// cancelling every in-flight asset runtime) and then re-added it, which both interrupted
        /// the asset task and could permanently drop the re-delivered asset. This test asserts that
        /// a device <c>Updated</c> notification refreshes the device in place and the asset keeps
        /// forwarding telemetry afterwards.
        /// </summary>
        [Fact]
        public async Task DeviceUpdatedKeepsAssetRuntimeAlive()
        {
            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAzureDeviceRegistryClientWrapper mockAdrClientWrapper = new MockAzureDeviceRegistryClientWrapper();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string datasetName = Guid.NewGuid().ToString();
            string expectedMqttTopic = "some/asset/telemetry/topic";

            Device device = CreateDevice(inboundEndpointName);
            Asset asset = CreateAsset(deviceName, inboundEndpointName, datasetName, expectedMqttTopic);

            TaskCompletionSource firstPublishTcs = new();
            TaskCompletionSource continuedAfterUpdateTcs = new();
            int publishCount = 0;
            int thresholdAfterUpdate = int.MaxValue;
            mockMqttClient.OnPublishAttempt += (msg) =>
            {
                if (string.Equals(msg.Topic, expectedMqttTopic))
                {
                    int count = Interlocked.Increment(ref publishCount);
                    firstPublishTcs.TrySetResult();
                    if (count >= Volatile.Read(ref thresholdAfterUpdate))
                    {
                        continuedAfterUpdateTcs.TrySetResult();
                    }
                }
                return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, "", new List<MqttUserProperty>()));
            };

            mockAdrClientWrapper.SimulateDeviceChanged(new(deviceName, inboundEndpointName, ChangeType.Created, device));
            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, assetName, ChangeType.Created, asset));

            // Asset is sampling and forwarding telemetry.
            await firstPublishTcs.Task.WaitAsync(WaitTimeout);

            // Simulate the device status echo that ADR re-delivers as a device Updated notification.
            mockAdrClientWrapper.SimulateDeviceChanged(new(deviceName, inboundEndpointName, ChangeType.Updated, device));

            // The asset runtime must survive the update and keep forwarding telemetry. Under the old
            // teardown-and-re-add behavior the mock ObserveAssets never re-delivers the asset, so no
            // further telemetry would ever arrive and this wait would time out.
            Volatile.Write(ref thresholdAfterUpdate, Volatile.Read(ref publishCount) + 3);
            await continuedAfterUpdateTcs.Task.WaitAsync(WaitTimeout);

            await worker.StopAsync(new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);
            worker.Dispose();
        }

        private static Device CreateDevice(string inboundEndpointName)
        {
            return new Device()
            {
                Endpoints = new()
                {
                    Inbound = new()
                    {
                        {
                            inboundEndpointName,
                            new()
                            {
                                Address = "someEndpointAddress",
                            }
                        }
                    }
                }
            };
        }

        private static Asset CreateAsset(string deviceName, string inboundEndpointName, string datasetName, string topic)
        {
            return new Asset()
            {
                DeviceRef = new()
                {
                    DeviceName = deviceName,
                    EndpointName = inboundEndpointName,
                },
                Datasets = new()
                {
                    {
                        new AssetDataset()
                        {
                            Name = datasetName,
                            DataPoints = new()
                            {
                                new AssetDatasetDataPoint()
                                {
                                    Name = "someDataPointName",
                                    DataSource = "someDataPointDataSource"
                                }
                            },
                            Destinations = new()
                            {
                                new DatasetDestination()
                                {
                                    Target = DatasetTarget.Mqtt,
                                    Configuration = new()
                                    {
                                        Topic = topic,
                                        Qos = QoS.Qos1
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
