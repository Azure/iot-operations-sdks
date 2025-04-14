// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using Azure.Iot.Operations.Services.Assets;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    public sealed class PollingTelemetryConnectorWorkerTests
    {
        [Fact]
        public async Task ConnectSingleAssetSingleDataset()
        {
            Environment.SetEnvironmentVariable("AEP_CONFIGMAP_MOUNT_PATH", "./TestMountFiles");

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAssetMonitor mockAssetMonitor = new MockAssetMonitor();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, mockAssetMonitor);
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
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}")
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

            await assetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public async Task ConnectSingleAssetMultipleDatasets()
        {
            Environment.SetEnvironmentVariable("AEP_CONFIGMAP_MOUNT_PATH", "./TestMountFiles");

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAssetMonitor mockAssetMonitor = new MockAssetMonitor();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, mockAssetMonitor);
            _ = worker.StartAsync(CancellationToken.None);
            var aep = new AssetEndpointProfile("localhost", "someAuthMethod", "someEndpointProfileType");
            mockAssetMonitor.AddOrUpdateMockAssetEndpointProfile(aep);
            string expectedMqttTopic1 = "some/asset/telemetry/topic1";
            string expectedMqttTopic2 = "some/asset/telemetry/topic2";
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
                            Path = expectedMqttTopic1,
                        },
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}")
                    },
                    new Dataset()
                    {
                        Name = "someOtherDataset",
                        DataPoints =
                        [
                            new DataPoint()
                            {
                                Name = "someOtherDataPoint",
                            }
                        ],
                        Topic = new()
                        {
                            Path = expectedMqttTopic2,
                        },
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}")
                    },
                ]
            };

            TaskCompletionSource assetDataset1TelemetryForwardedToBrokerTcs = new();
            TaskCompletionSource assetDataset2TelemetryForwardedToBrokerTcs = new();
            mockMqttClient.OnPublishAttempt += (msg) =>
            {
                if (string.Equals(msg.Topic, expectedMqttTopic1))
                {
                    assetDataset1TelemetryForwardedToBrokerTcs.TrySetResult();
                }
                else if (string.Equals(msg.Topic, expectedMqttTopic2))
                {
                    assetDataset2TelemetryForwardedToBrokerTcs.TrySetResult();
                }
                return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, "", new List<MqttUserProperty>()));
            };

            mockAssetMonitor.AddOrUpdateMockAsset("someAsset", asset);

            await assetDataset1TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await assetDataset2TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public async Task ConnectMultipleAssetsSingleDataset()
        {
            Environment.SetEnvironmentVariable("AEP_CONFIGMAP_MOUNT_PATH", "./TestMountFiles");

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAssetMonitor mockAssetMonitor = new MockAssetMonitor();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, mockAssetMonitor);
            _ = worker.StartAsync(CancellationToken.None);
            var aep = new AssetEndpointProfile("localhost", "someAuthMethod", "someEndpointProfileType");
            mockAssetMonitor.AddOrUpdateMockAssetEndpointProfile(aep);
            string expectedMqttTopic1 = "some/asset/telemetry/topic1";
            string expectedMqttTopic2 = "some/asset/telemetry/topic2";
            var asset1 = new Asset()
            {
                Datasets =
                [
                    new Dataset()
                    {
                        Name = "someDataset1",
                        DataPoints =
                        [
                            new DataPoint()
                            {
                                Name = "someDataPoint1",
                            }
                        ],
                        Topic = new()
                        {
                            Path = expectedMqttTopic1,
                        },
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}")
                    }
                ]
            };

            var asset2 = new Asset()
            {
                Datasets =
                [
                    new Dataset()
                    {
                        Name = "someDataset2",
                        DataPoints =
                        [
                            new DataPoint()
                            {
                                Name = "someDataPoint2",
                            }
                        ],
                        Topic = new()
                        {
                            Path = expectedMqttTopic2,
                        },
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}")
                    }
                ]
            };

            TaskCompletionSource asset1TelemetryForwardedToBrokerTcs = new();
            TaskCompletionSource asset2TelemetryForwardedToBrokerTcs = new();
            mockMqttClient.OnPublishAttempt += (msg) =>
            {
                if (string.Equals(msg.Topic, expectedMqttTopic1))
                {
                    asset1TelemetryForwardedToBrokerTcs.TrySetResult();
                }
                else if (string.Equals(msg.Topic, expectedMqttTopic2))
                {
                    asset2TelemetryForwardedToBrokerTcs.TrySetResult();
                }
                return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, "", new List<MqttUserProperty>()));
            };

            mockAssetMonitor.AddOrUpdateMockAsset("someAsset1", asset1);
            mockAssetMonitor.AddOrUpdateMockAsset("someAsset2", asset2);

            await asset1TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await asset2TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public async Task ConnectMultipleAssetsMultipleDataset()
        {
            Environment.SetEnvironmentVariable("AEP_CONFIGMAP_MOUNT_PATH", "./TestMountFiles");

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAssetMonitor mockAssetMonitor = new MockAssetMonitor();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, mockAssetMonitor);
            _ = worker.StartAsync(CancellationToken.None);
            var aep = new AssetEndpointProfile("localhost", "someAuthMethod", "someEndpointProfileType");
            mockAssetMonitor.AddOrUpdateMockAssetEndpointProfile(aep);
            string expectedMqttTopic1 = "some/asset/telemetry/topic1";
            string expectedMqttTopic2 = "some/asset/telemetry/topic2";
            string expectedMqttTopic3 = "some/asset/telemetry/topic3";
            string expectedMqttTopic4 = "some/asset/telemetry/topic4";
            var asset1 = new Asset()
            {
                Datasets =
                [
                    new Dataset()
                    {
                        Name = "someDataset1",
                        DataPoints =
                        [
                            new DataPoint()
                            {
                                Name = "someDataPoint1",
                            }
                        ],
                        Topic = new()
                        {
                            Path = expectedMqttTopic1,
                        },
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}")
                    },
                    new Dataset()
                    {
                        Name = "someDataset2",
                        DataPoints =
                        [
                            new DataPoint()
                            {
                                Name = "someDataPoint2",
                            }
                        ],
                        Topic = new()
                        {
                            Path = expectedMqttTopic2,
                        },
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}")
                    }
                ]
            };

            var asset2 = new Asset()
            {
                Datasets =
                [
                    new Dataset()
                    {
                        Name = "someDataset3",
                        DataPoints =
                        [
                            new DataPoint()
                            {
                                Name = "someDataPoint3",
                            }
                        ],
                        Topic = new()
                        {
                            Path = expectedMqttTopic3,
                        },
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}")
                    },
                    new Dataset()
                    {
                        Name = "someDataset4",
                        DataPoints =
                        [
                            new DataPoint()
                            {
                                Name = "someDataPoint4",
                            }
                        ],
                        Topic = new()
                        {
                            Path = expectedMqttTopic4,
                        },
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}")
                    }
                ]
            };

            TaskCompletionSource asset1Dataset1TelemetryForwardedToBrokerTcs = new();
            TaskCompletionSource asset1Dataset2TelemetryForwardedToBrokerTcs = new();
            TaskCompletionSource asset2Dataset1TelemetryForwardedToBrokerTcs = new();
            TaskCompletionSource asset2Dataset2TelemetryForwardedToBrokerTcs = new();
            mockMqttClient.OnPublishAttempt += (msg) =>
            {
                if (string.Equals(msg.Topic, expectedMqttTopic1))
                {
                    asset1Dataset1TelemetryForwardedToBrokerTcs.TrySetResult();
                }
                else if (string.Equals(msg.Topic, expectedMqttTopic2))
                {
                    asset1Dataset2TelemetryForwardedToBrokerTcs.TrySetResult();
                }
                else if (string.Equals(msg.Topic, expectedMqttTopic3))
                {
                    asset2Dataset1TelemetryForwardedToBrokerTcs.TrySetResult();
                }
                else if (string.Equals(msg.Topic, expectedMqttTopic4))
                {
                    asset2Dataset2TelemetryForwardedToBrokerTcs.TrySetResult();
                }
                return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, "", new List<MqttUserProperty>()));
            };

            mockAssetMonitor.AddOrUpdateMockAsset("someAsset1", asset1);
            mockAssetMonitor.AddOrUpdateMockAsset("someAsset2", asset2);

            await asset1Dataset1TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await asset1Dataset2TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await asset2Dataset1TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await asset2Dataset2TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public async Task DeletedAssetStopsSampling()
        {
            Environment.SetEnvironmentVariable("AEP_CONFIGMAP_MOUNT_PATH", "./TestMountFiles");

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAssetMonitor mockAssetMonitor = new MockAssetMonitor();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, mockAssetMonitor);
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
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}")
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

            string assetName = "someAsset";
            mockAssetMonitor.AddOrUpdateMockAsset(assetName, asset);

            // Asset has been added and telemetry is being forwarded. Now we can remove the asset and check that telemetry stops flowing
            await assetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            mockAssetMonitor.DeleteMockAsset(assetName);

            assetTelemetryForwardedToBrokerTcs = new();

            // Wait a bit for the asset deletion to take effect since sampling may have been in progress.
            await Task.Delay(TimeSpan.FromSeconds(1));

            await Assert.ThrowsAsync<TimeoutException>(async () => await assetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        }

        [Fact]
        public async Task UpdateSingleAssetSingleDataset()
        {
            Environment.SetEnvironmentVariable("AEP_CONFIGMAP_MOUNT_PATH", "./TestMountFiles");

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAssetMonitor mockAssetMonitor = new MockAssetMonitor();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, mockAssetMonitor);
            _ = worker.StartAsync(CancellationToken.None);
            var aep = new AssetEndpointProfile("localhost", "someAuthMethod", "someEndpointProfileType");
            mockAssetMonitor.AddOrUpdateMockAssetEndpointProfile(aep);
            string expectedMqttTopic1 = "some/asset/telemetry/topic1";
            string expectedMqttTopic2 = "some/asset/telemetry/topic2";
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
                            Path = expectedMqttTopic1,
                        },
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}")
                    }
                ]
            };

            TaskCompletionSource assetTelemetryForwardedToBrokerTcs1 = new();
            TaskCompletionSource assetTelemetryForwardedToBrokerTcs2 = new();
            mockMqttClient.OnPublishAttempt += (msg) =>
            {
                if (string.Equals(msg.Topic, expectedMqttTopic1))
                {
                    assetTelemetryForwardedToBrokerTcs1.TrySetResult();
                }
                else if (string.Equals(msg.Topic, expectedMqttTopic2))
                {
                    assetTelemetryForwardedToBrokerTcs2.TrySetResult();
                }
                return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, "", new List<MqttUserProperty>()));
            };

            string assetName = "someAsset";
            mockAssetMonitor.AddOrUpdateMockAsset(assetName, asset);

            // Asset has been added and telemetry is being forwarded. Now we can update the asset and check that telemetry
            // starts flowing on the new MQTT topic
            await assetTelemetryForwardedToBrokerTcs1.Task.WaitAsync(TimeSpan.FromSeconds(3));

            Assert.NotNull(asset);
            Assert.NotNull(asset.Datasets[0]);
            Assert.NotNull(asset.Datasets[0].Topic);
            asset.Datasets[0].Topic!.Path = expectedMqttTopic2;
            mockAssetMonitor.AddOrUpdateMockAsset(assetName, asset);

            await assetTelemetryForwardedToBrokerTcs2.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public async Task DatasetUsesDefaultsIfNoDatasetSpecificValuesConfigured()
        {
            Environment.SetEnvironmentVariable("AEP_CONFIGMAP_MOUNT_PATH", "./TestMountFiles");

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAssetMonitor mockAssetMonitor = new MockAssetMonitor();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, mockAssetMonitor);
            _ = worker.StartAsync(CancellationToken.None);
            var aep = new AssetEndpointProfile("localhost", "someAuthMethod", "someEndpointProfileType");
            mockAssetMonitor.AddOrUpdateMockAssetEndpointProfile(aep);
            string expectedMqttTopic = "some/asset/telemetry/topic";
            var asset = new Asset()
            {
                DefaultDatasetsConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}"),
                DefaultTopic = new()
                {
                    Path = expectedMqttTopic,
                },
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

            await assetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public async Task ConnectorRecoversFromSamplingErrors()
        {
            Environment.SetEnvironmentVariable("AEP_CONFIGMAP_MOUNT_PATH", "./TestMountFiles");

            // This dataset sampler factory will create a faulty dataset sampler that fails to sample the dataset
            // for the first few attempts.
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory(true);

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAssetMonitor mockAssetMonitor = new MockAssetMonitor();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, mockAssetMonitor);
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
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}")
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

            await assetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }

        [Fact]
        public async Task DeletingAssetEndpointProfileStopsSampling()
        {
            Environment.SetEnvironmentVariable("AEP_CONFIGMAP_MOUNT_PATH", "./TestMountFiles");

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAssetMonitor mockAssetMonitor = new MockAssetMonitor();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, mockAssetMonitor);
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
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}")
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

            await assetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            // At this point, the connector app is actively sampling some dataset, so we can delete the asset endpoint
            // profile and confirm that sampling stops.

            mockAssetMonitor.DeleteMockAssetEndpointProfile();

            // Allow a grace period for the connector to shut everything down
            await Task.Delay(TimeSpan.FromSeconds(3));

            // No more telemetry should be flowing
            assetTelemetryForwardedToBrokerTcs = new();
            await Assert.ThrowsAsync<TimeoutException>(async () => await assetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        }

        [Fact]
        public async Task DeletingSingleAssetDoesNotStopSamplingOfOtherAsset()
        {
            Environment.SetEnvironmentVariable("AEP_CONFIGMAP_MOUNT_PATH", "./TestMountFiles");

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAssetMonitor mockAssetMonitor = new MockAssetMonitor();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, mockAssetMonitor);
            _ = worker.StartAsync(CancellationToken.None);
            var aep = new AssetEndpointProfile("localhost", "someAuthMethod", "someEndpointProfileType");
            mockAssetMonitor.AddOrUpdateMockAssetEndpointProfile(aep);
            string expectedMqttTopic1 = "some/asset/telemetry/topic1";
            string expectedMqttTopic2 = "some/asset/telemetry/topic2";
            var asset1 = new Asset()
            {
                Datasets =
                [
                    new Dataset()
                    {
                        Name = "someDataset1",
                        DataPoints =
                        [
                            new DataPoint()
                            {
                                Name = "someDataPoint1",
                            }
                        ],
                        Topic = new()
                        {
                            Path = expectedMqttTopic1,
                        },
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}")
                    }
                ]
            };

            var asset2 = new Asset()
            {
                Datasets =
                [
                    new Dataset()
                    {
                        Name = "someDataset2",
                        DataPoints =
                        [
                            new DataPoint()
                            {
                                Name = "someDataPoint2",
                            }
                        ],
                        Topic = new()
                        {
                            Path = expectedMqttTopic2,
                        },
                        DatasetConfiguration = JsonDocument.Parse("{\"samplingInterval\": 100}")
                    }
                ]
            };

            TaskCompletionSource asset1TelemetryForwardedToBrokerTcs = new();
            TaskCompletionSource asset2TelemetryForwardedToBrokerTcs = new();
            mockMqttClient.OnPublishAttempt += (msg) =>
            {
                if (string.Equals(msg.Topic, expectedMqttTopic1))
                {
                    asset1TelemetryForwardedToBrokerTcs.TrySetResult();
                }
                else if (string.Equals(msg.Topic, expectedMqttTopic2))
                {
                    asset2TelemetryForwardedToBrokerTcs.TrySetResult();
                }
                return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, "", new List<MqttUserProperty>()));
            };

            mockAssetMonitor.AddOrUpdateMockAsset("someAsset1", asset1);
            mockAssetMonitor.AddOrUpdateMockAsset("someAsset2", asset2);

            await asset1TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await asset2TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            // At this point, the connector app is actively sampling both assets. Deleting one asset should
            // cause the connector to stop sampling that asset, but the other asset should continue to be sampled.

            mockAssetMonitor.DeleteMockAsset("someAsset1");

            asset1TelemetryForwardedToBrokerTcs = new();

            // Wait a bit for the asset deletion to take effect since sampling may have been in progress.
            await Task.Delay(TimeSpan.FromSeconds(1));

            await Assert.ThrowsAsync<TimeoutException>(async () => await asset1TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3)));

            // The remaining asset should still be publishing telemetry
            asset2TelemetryForwardedToBrokerTcs = new();
            await asset2TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }
}
