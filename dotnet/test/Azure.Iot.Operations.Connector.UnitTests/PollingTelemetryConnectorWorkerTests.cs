﻿// Copyright (c) Microsoft Corporation.
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
    // These tests rely on environment variables which may intefere with other similar tests
    [Collection("Environment Variable Sequential")]
    public sealed class PollingTelemetryConnectorWorkerTests
    {
        public PollingTelemetryConnectorWorkerTests()
        {
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.ConnectorConfigMountPathEnvVar, "./connector-config-no-auth-no-tls");
            Environment.SetEnvironmentVariable(ConnectorFileMountSettings.ConnectorClientIdEnvVar, "someClientId");
        }

        private class MockAdrClientFactory : IAdrClientWrapperProvider
        {
            private readonly IAdrClientWrapper _mockAdrClientWrapper;

            public MockAdrClientFactory(IAdrClientWrapper mockAdrClientWrapper)
            {
                _mockAdrClientWrapper = mockAdrClientWrapper;
            }

            public IAdrClientWrapper CreateAdrClientWrapper(ApplicationContext applicationContext, IMqttPubSubClient mqttPubSubClient)
            {
                return _mockAdrClientWrapper;
            }
        }

        [Fact]
        public async Task ConnectSingleDeviceSingleAssetSingleDatasetSingleDatapointSingleDestination()
        {
            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAdrClientWrapper mockAdrClientWrapper = new MockAdrClientWrapper();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string datasetName = Guid.NewGuid().ToString();

            var device = new Device()
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

            mockAdrClientWrapper.SimulateDeviceChanged(new(deviceName, inboundEndpointName, ChangeType.Created, device));

            string expectedMqttTopic = "some/asset/telemetry/topic";
            var asset = new Asset()
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
                                    new AssetDatasetDataPointSchemaElement()
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
                                            Topic = expectedMqttTopic,
                                            Qos = QoS.Qos1
                                        }
                                    }
                                }
                            }
                        }
                    }
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

            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, assetName, ChangeType.Created, asset));

            await assetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        [Fact]
        public async Task ConnectSingleDeviceSingleAssetSingleDatasetSingleDataPointMultipleDestinations()
        {
            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAdrClientWrapper mockAdrClientWrapper = new MockAdrClientWrapper();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string datasetName = Guid.NewGuid().ToString();

            var device = new Device()
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

            mockAdrClientWrapper.SimulateDeviceChanged(new(deviceName, inboundEndpointName, ChangeType.Created, device));

            string expectedMqttTopic = "some/asset/telemetry/topic1";
            string expectedStateStoreTopicString = "statestore/v1/FA9AE35F-2F64-47CD-9BFF-08E2B32A0FE8/command/invoke";
            string expectedStateStoreKey = Guid.NewGuid().ToString();
            var asset = new Asset()
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
                                    new AssetDatasetDataPointSchemaElement()
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
                                            Topic = expectedMqttTopic,
                                            Qos = QoS.Qos1
                                        }
                                    },
                                    new DatasetDestination()
                                    {
                                        Target = DatasetTarget.BrokerStateStore,
                                        Configuration = new()
                                        {
                                            Key = expectedStateStoreKey,
                                        }
                                    }
                                }
                            }
                        }
                    }
            };

            TaskCompletionSource telemetryForwardedToMqttBrokerTcs = new();
            TaskCompletionSource stateStoreKeyForwardedTcs = new();
            mockMqttClient.OnPublishAttempt += (msg) =>
            {
                if (string.Equals(msg.Topic, expectedMqttTopic))
                {
                    telemetryForwardedToMqttBrokerTcs.TrySetResult();
                }
                else if (string.Equals(msg.Topic, expectedStateStoreTopicString))
                {
                    stateStoreKeyForwardedTcs.TrySetResult();
                }

                return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, "", new List<MqttUserProperty>()));
            };

            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, assetName, ChangeType.Created, asset));

            await telemetryForwardedToMqttBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await stateStoreKeyForwardedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        [Fact]
        public async Task ConnectSingleDeviceMultipleAssetsSingleDatasetSingleDatapointSingleDestination()
        {
            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAdrClientWrapper mockAdrClientWrapper = new MockAdrClientWrapper();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetNamePrefix = Guid.NewGuid().ToString();
            string datasetName = Guid.NewGuid().ToString();

            var device = new Device()
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

            mockAdrClientWrapper.SimulateDeviceChanged(new(deviceName, inboundEndpointName, ChangeType.Created, device));

            List<string> expectedMqttTopics = new();
            List<Asset> assets = new();
            for (int i = 0; i < 2; i++)
            {
                expectedMqttTopics.Add("some/asset/telemetry/topic" + i);
                assets.Add(new Asset()
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
                                    new AssetDatasetDataPointSchemaElement()
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
                                            Topic = expectedMqttTopics[i],
                                            Qos = QoS.Qos1
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }


            TaskCompletionSource asset1TelemetryForwardedToBrokerTcs = new();
            TaskCompletionSource asset2TelemetryForwardedToBrokerTcs = new();
            mockMqttClient.OnPublishAttempt += (msg) =>
            {
                if (string.Equals(msg.Topic, expectedMqttTopics[0]))
                {
                    asset1TelemetryForwardedToBrokerTcs.TrySetResult();
                }
                else if (string.Equals(msg.Topic, expectedMqttTopics[1]))
                {
                    asset2TelemetryForwardedToBrokerTcs.TrySetResult();
                }

                return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, "", new List<MqttUserProperty>()));
            };

            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, assetNamePrefix + 0, ChangeType.Created, assets[0]));
            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, assetNamePrefix + 1, ChangeType.Created, assets[1]));

            await asset1TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await asset2TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        [Fact]
        public async Task ConnectMultipleDevicesSingleAssetSingleDatasetSingleDatapointSingleDestination()
        {
            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAdrClientWrapper mockAdrClientWrapper = new MockAdrClientWrapper();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

            string device1Name = Guid.NewGuid().ToString();
            string device2Name = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string datasetName = Guid.NewGuid().ToString();

            var device1 = new Device()
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

            var device2 = new Device()
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

            mockAdrClientWrapper.SimulateDeviceChanged(new(device1Name, inboundEndpointName, ChangeType.Created, device1));
            mockAdrClientWrapper.SimulateDeviceChanged(new(device2Name, inboundEndpointName, ChangeType.Created, device2));

            string expectedMqttTopic1 = "some/asset/telemetry/topic1";
            string expectedMqttTopic2 = "some/asset/telemetry/topic2";
            var asset1 = new Asset()
            {
                DeviceRef = new()
                {
                    DeviceName = device1Name,
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
                                    new AssetDatasetDataPointSchemaElement()
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
                                            Topic = expectedMqttTopic1,
                                            Qos = QoS.Qos1
                                        }
                                    }
                                }
                            }
                        }
                    }
            };

            var asset2 = new Asset()
            {
                DeviceRef = new()
                {
                    DeviceName = device2Name,
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
                                    new AssetDatasetDataPointSchemaElement()
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
                                            Topic = expectedMqttTopic2,
                                            Qos = QoS.Qos1
                                        }
                                    }
                                }
                            }
                        }
                    }
            };

            TaskCompletionSource device1AssetTelemetryForwardedToBrokerTcs = new();
            TaskCompletionSource device2AssetTelemetryForwardedToBrokerTcs = new();
            mockMqttClient.OnPublishAttempt += (msg) =>
            {
                if (string.Equals(msg.Topic, expectedMqttTopic1))
                {
                    device1AssetTelemetryForwardedToBrokerTcs.TrySetResult();
                }
                else if (string.Equals(msg.Topic, expectedMqttTopic2))
                {
                    device2AssetTelemetryForwardedToBrokerTcs.TrySetResult();
                }

                return Task.FromResult(new MqttClientPublishResult(0, MqttClientPublishReasonCode.Success, "", new List<MqttUserProperty>()));
            };

            mockAdrClientWrapper.SimulateAssetChanged(new(device1Name, inboundEndpointName, assetName, ChangeType.Created, asset1));
            mockAdrClientWrapper.SimulateAssetChanged(new(device2Name, inboundEndpointName, assetName, ChangeType.Created, asset2));

            await device1AssetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await device2AssetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        [Fact]
        public async Task DeletedAssetStopsSampling()
        {
            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAdrClientWrapper mockAdrClientWrapper = new MockAdrClientWrapper();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string datasetName = Guid.NewGuid().ToString();

            var device = new Device()
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

            mockAdrClientWrapper.SimulateDeviceChanged(new(deviceName, inboundEndpointName, ChangeType.Created, device));

            string expectedMqttTopic = "some/asset/telemetry/topic";
            var asset = new Asset()
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
                                    new AssetDatasetDataPointSchemaElement()
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
                                            Topic = expectedMqttTopic,
                                            Qos = QoS.Qos1
                                        }
                                    }
                                }
                            }
                        }
                    }
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

            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, assetName, ChangeType.Created, asset));

            // Asset has been added and telemetry is being forwarded. Now we can remove the asset and check that telemetry stops flowing
            await assetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, assetName, ChangeType.Deleted, null));

            assetTelemetryForwardedToBrokerTcs = new();

            // Wait a bit for the asset deletion to take effect since sampling may have been in progress.
            await Task.Delay(TimeSpan.FromSeconds(1));

            await Assert.ThrowsAsync<TimeoutException>(async () => await assetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3)));

            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        [Fact]
        public async Task DeletedDeviceStopsSampling()
        {
            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAdrClientWrapper mockAdrClientWrapper = new MockAdrClientWrapper();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string datasetName = Guid.NewGuid().ToString();

            var device = new Device()
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

            mockAdrClientWrapper.SimulateDeviceChanged(new(deviceName, inboundEndpointName, ChangeType.Created, device));

            string expectedMqttTopic = "some/asset/telemetry/topic";
            var asset = new Asset()
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
                                    new AssetDatasetDataPointSchemaElement()
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
                                            Topic = expectedMqttTopic,
                                            Qos = QoS.Qos1
                                        }
                                    }
                                }
                            }
                        }
                    }
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

            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, assetName, ChangeType.Created, asset));

            // Asset has been added and telemetry is being forwarded. Now we can remove the asset and check that telemetry stops flowing
            await assetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            mockAdrClientWrapper.SimulateDeviceChanged(new(deviceName, inboundEndpointName, ChangeType.Deleted, null));

            // Wait a bit for the asset deletion to take effect since sampling may have been in progress.
            await Task.Delay(TimeSpan.FromSeconds(1));

            assetTelemetryForwardedToBrokerTcs = new();

            await Assert.ThrowsAsync<TimeoutException>(async () => await assetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3)));

            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        [Fact]
        public async Task UpdatedAssetContinuesSampling()
        {
            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAdrClientWrapper mockAdrClientWrapper = new MockAdrClientWrapper();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string datasetName = Guid.NewGuid().ToString();

            var device = new Device()
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

            mockAdrClientWrapper.SimulateDeviceChanged(new(deviceName, inboundEndpointName, ChangeType.Created, device));

            string expectedMqttTopic1 = "some/asset/telemetry/topic1";
            string expectedMqttTopic2 = "some/asset/telemetry/topic2";
            var asset = new Asset()
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
                                    new AssetDatasetDataPointSchemaElement()
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
                                            Topic = expectedMqttTopic1,
                                            Qos = QoS.Qos1
                                        }
                                    }
                                }
                            }
                        }
                    }
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

            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, assetName, ChangeType.Created, asset));

            // Asset has been added and telemetry is being forwarded. Now we can update the asset and check that telemetry starts flowing to the updated topic
            await asset1TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            asset.Datasets![0].Destinations![0].Configuration.Topic = expectedMqttTopic2;
            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, assetName, ChangeType.Updated, asset));

            await asset2TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        [Fact]
        public async Task ConnectorRecoversFromSamplingErrors()
        {
            // This dataset sampler factory will create a faulty dataset sampler that fails to sample the dataset
            // for the first few attempts.
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory(true);

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAdrClientWrapper mockAdrClientWrapper = new MockAdrClientWrapper();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string datasetName = Guid.NewGuid().ToString();

            var device = new Device()
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

            mockAdrClientWrapper.SimulateDeviceChanged(new(deviceName, inboundEndpointName, ChangeType.Created, device));

            string expectedMqttTopic = "some/asset/telemetry/topic";
            var asset = new Asset()
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
                                    new AssetDatasetDataPointSchemaElement()
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
                                            Topic = expectedMqttTopic,
                                            Qos = QoS.Qos1
                                        }
                                    }
                                }
                            }
                        }
                    }
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

            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, assetName, ChangeType.Created, asset));

            await assetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        [Fact]
        public async Task DeletingSingleAssetDoesNotStopSamplingOfOtherAsset()
        {
            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAdrClientWrapper mockAdrClientWrapper = new MockAdrClientWrapper();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string asset1Name = Guid.NewGuid().ToString();
            string asset2Name = Guid.NewGuid().ToString();
            string datasetName = Guid.NewGuid().ToString();

            var device = new Device()
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

            mockAdrClientWrapper.SimulateDeviceChanged(new(deviceName, inboundEndpointName, ChangeType.Created, device));

            string expectedMqttTopic1 = "some/asset/telemetry/topic1";
            string expectedMqttTopic2 = "some/asset/telemetry/topic2";
            var asset1 = new Asset()
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
                                    new AssetDatasetDataPointSchemaElement()
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
                                            Topic = expectedMqttTopic1,
                                            Qos = QoS.Qos1
                                        }
                                    }
                                }
                            }
                        }
                    }
            };

            var asset2 = new Asset()
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
                                    new AssetDatasetDataPointSchemaElement()
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
                                            Topic = expectedMqttTopic2,
                                            Qos = QoS.Qos1
                                        }
                                    }
                                }
                            }
                        }
                    }
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

            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, asset1Name, ChangeType.Created, asset1));
            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, asset2Name, ChangeType.Created, asset2));

            // Both assets should be polling now
            await asset1TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await asset2TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, asset1Name, ChangeType.Deleted, null));

            // Wait a bit for the asset deletion to take effect since sampling may have been in progress.
            await Task.Delay(TimeSpan.FromSeconds(1));

            asset2TelemetryForwardedToBrokerTcs = new();

            // asset 2 telemetry should still be flowing since the deleted asset was asset 1
            await asset2TelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        [Fact]
        public async Task UnrelatedAssetDoesNotCrashConnector()
        {
            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAdrClientWrapper mockAdrClientWrapper = new MockAdrClientWrapper();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string datasetName = Guid.NewGuid().ToString();

            string expectedMqttTopic = "some/asset/telemetry/topic";
            var unexpectedAsset = new Asset()
            {
                DeviceRef = new()
                {
                    DeviceName = Guid.NewGuid().ToString(),
                    EndpointName = Guid.NewGuid().ToString(),
                },
                Datasets = new()
                    {
                        {
                            new AssetDataset()
                            {
                                Name = datasetName,
                                DataPoints = new()
                                {
                                    new AssetDatasetDataPointSchemaElement()
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
                                            Topic = expectedMqttTopic,
                                            Qos = QoS.Qos1
                                        }
                                    }
                                }
                            }
                        }
                    }
            };

            // Simulate an asset being added, but the asset belongs to a device that does not exist
            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, assetName, ChangeType.Created, unexpectedAsset));

            var device = new Device()
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

            mockAdrClientWrapper.SimulateDeviceChanged(new(deviceName, inboundEndpointName, ChangeType.Created, device));

            var asset = new Asset()
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
                                    new AssetDatasetDataPointSchemaElement()
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
                                            Topic = expectedMqttTopic,
                                            Qos = QoS.Qos1
                                        }
                                    }
                                }
                            }
                        }
                    }
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

            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, assetName, ChangeType.Created, asset));

            await assetTelemetryForwardedToBrokerTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        // User-provided callbacks may still be running after the connector worker cancels them. We need to check that the connector
        // doesn't complete its shutdown before all of its user-supplied tasks have finished
        [Fact]
        public async Task ConnectorWaitsForUserCallbacksToComplete()
        {
            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAdrClientWrapper mockAdrClientWrapper = new MockAdrClientWrapper();
            IDatasetSamplerFactory mockDatasetSamplerFactory = new MockDatasetSamplerFactory();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            PollingTelemetryConnectorWorker worker = new PollingTelemetryConnectorWorker(new Protocol.ApplicationContext(), mockLogger.Object, mockMqttClient, mockDatasetSamplerFactory, messageSchemaProviderFactory, new MockAdrClientFactory(mockAdrClientWrapper));

            TaskCompletionSource cancellationTokenTriggeredInDeviceCallback = new();
            TaskCompletionSource cancellationTokenTriggeredInAssetCallback = new();
            TaskCompletionSource deviceCallbackEndedGracefully = new();
            TaskCompletionSource assetCallbackEndedGracefully = new();

            worker.WhileDeviceIsAvailable += async (args, cancellationToken) =>
            {
                try
                {
                    // cancellation token should trigger almost immediately
                    await Task.Delay(TimeSpan.FromHours(24), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // expected exception
                    cancellationTokenTriggeredInDeviceCallback.TrySetResult();
                }

                // simulate the device task running longer than expected after cancellation
                await Task.Delay(TimeSpan.FromSeconds(1));
                deviceCallbackEndedGracefully.TrySetResult();
            };

            worker.WhileAssetIsAvailable += async (args, cancellationToken) =>
            {
                try
                {
                    // cancellation token should trigger almost immediately
                    await Task.Delay(TimeSpan.FromHours(24), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // expected exception
                    cancellationTokenTriggeredInAssetCallback.TrySetResult();
                }

                // simulate the asset task running longer than expected after cancellation
                await Task.Delay(TimeSpan.FromSeconds(1));
                assetCallbackEndedGracefully.TrySetResult();
            };

            Task workerTask = worker.StartAsync(CancellationToken.None);

            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string datasetName = Guid.NewGuid().ToString();

            var device = new Device()
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

            mockAdrClientWrapper.SimulateDeviceChanged(new(deviceName, inboundEndpointName, ChangeType.Created, device));

            string expectedMqttTopic = "some/asset/telemetry/topic";
            var asset = new Asset()
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
                                    new AssetDatasetDataPointSchemaElement()
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
                                            Topic = expectedMqttTopic,
                                            Qos = QoS.Qos1
                                        }
                                    }
                                }
                            }
                        }
                    }
            };

            mockAdrClientWrapper.SimulateAssetChanged(new(deviceName, inboundEndpointName, assetName, ChangeType.Created, asset));

            await worker.StopAsync(CancellationToken.None);

            // The user callbacks should each trigger the provided cancellation token and should end gracefully
            try
            {
                await cancellationTokenTriggeredInDeviceCallback.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await cancellationTokenTriggeredInAssetCallback.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                Assert.Fail("User-supplied callbacks did not get canceled as expected");
            }

            try
            {
                await deviceCallbackEndedGracefully.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await assetCallbackEndedGracefully.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                Assert.Fail("User-supplied callbacks were cancelled as expected but weren't awaited");
            }


            worker.Dispose();
        }
    }
}
