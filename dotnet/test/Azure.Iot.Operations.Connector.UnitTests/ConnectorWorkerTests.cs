// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.CloudEvents;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MessageSchemaReference = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models.MessageSchemaReference;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    /// <summary>
    /// Tests for verifying that protocolSpecificIdentifier is used correctly in CloudEvent construction
    /// and that endpoint address is NOT used as a fallback after the removal of defaulting logic.
    /// </summary>
    public sealed class ConnectorWorkerTests
    {
        private const string TestEndpointAddress = "endpoint-address-should-not-be-used";
        private const string TestProtocolIdentifier = "custom-protocol-identifier";
        private const string TestDeviceName = "test-device";
        private const string TestDeviceUuid = "device-uuid-123";
        private const string TestInboundEndpointName = "test-endpoint";
        private const string TestAssetName = "test-asset";
        private const string TestAssetUuid = "asset-uuid-456";
        private const string TestDatasetName = "test-dataset";
        private const string TestEventGroupName = "test-event-group";

        private class MockAdrClientFactory : IAzureDeviceRegistryClientWrapperProvider
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

        [Fact]
        public void AioCloudEventBuilder_Dataset_WithExplicitProtocolIdentifier_UsesProvidedIdentifierInSource()
        {
            // Arrange
            Device device = CreateTestDevice();
            Asset asset = CreateTestAsset();
            AssetDataset dataset = new AssetDataset { Name = TestDatasetName };
            Schema schema = new Schema
            {
                Name = "test-schema",
                Version = "1.0.0",
                Format = Format.JsonSchemaDraft07,
                SchemaType = SchemaType.MessageSchema,
            };

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAzureDeviceRegistryClientWrapper mockAdrClientWrapper = new MockAzureDeviceRegistryClientWrapper();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            ConnectorWorker worker = new ConnectorWorker(new ApplicationContext(),
                mockLogger.Object,
                mockMqttClient,
                messageSchemaProviderFactory,
                new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

            // Act
            var result = worker.ConstructCloudEventHeadersForDataset(
                device,
                TestDeviceName,
                TestInboundEndpointName,
                asset,
                TestAssetName,
                dataset,
                schema,
                protocolSpecificIdentifier: TestProtocolIdentifier);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Source);
            Assert.Contains(TestProtocolIdentifier, result.Source.ToString());
            Assert.DoesNotContain(TestEndpointAddress, result.Source.ToString());
        }

        [Fact]
        public void AioCloudEventBuilder_Dataset_WithoutProtocolIdentifier_DoesNotUseEndpointAddress()
        {
            // Arrange
            Device device = CreateTestDevice();
            Asset asset = CreateTestAsset();
            AssetDataset dataset = new AssetDataset { Name = TestDatasetName };
            Schema schema = new Schema
            {
                Name = "test-schema",
                Version = "1.0.0",
                Format = Format.JsonSchemaDraft07,
                SchemaType = SchemaType.MessageSchema,
            };

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAzureDeviceRegistryClientWrapper mockAdrClientWrapper = new MockAzureDeviceRegistryClientWrapper();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            ConnectorWorker worker = new ConnectorWorker(new ApplicationContext(),
                mockLogger.Object,
                mockMqttClient,
                messageSchemaProviderFactory,
                new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

            // Act
            var result = worker.ConstructCloudEventHeadersForDataset(
                device,
                TestDeviceName,
                TestInboundEndpointName,
                asset,
                TestAssetName,
                dataset,
                schema,
                protocolSpecificIdentifier: null);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Source);
            Assert.DoesNotContain(TestEndpointAddress, result.Source.ToString());
        }

        [Fact]
        public void AioCloudEventBuilder_Event_WithExplicitProtocolIdentifier_UsesProvidedIdentifierInSource()
        {
            // Arrange
            Device device = CreateTestDevice();
            Asset asset = CreateTestAsset();
            Schema schema = new Schema
            {
                Name = "test-schema",
                Version = "1.0.0",
                Format = Format.JsonSchemaDraft07,
                SchemaType = SchemaType.MessageSchema,
            };

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAzureDeviceRegistryClientWrapper mockAdrClientWrapper = new MockAzureDeviceRegistryClientWrapper();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            ConnectorWorker worker = new ConnectorWorker(new ApplicationContext(),
                mockLogger.Object,
                mockMqttClient,
                messageSchemaProviderFactory,
                new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

            // Act
            AssetEvent assetEvent = CreateTestAssetEvent();
            var result = worker.ConstructCloudEventHeadersForEvent(
                device,
                TestDeviceName,
                TestInboundEndpointName,
                asset,
                TestAssetName,
                TestEventGroupName,
                assetEvent,
                schema,
                protocolSpecificIdentifier: TestProtocolIdentifier);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Source);
            Assert.Contains(TestProtocolIdentifier, result.Source.ToString());
            Assert.DoesNotContain(TestEndpointAddress, result.Source.ToString());
        }

        [Fact]
        public void AioCloudEventBuilder_Event_WithoutProtocolIdentifier_DoesNotUseEndpointAddress()
        {
            // Arrange
            Device device = CreateTestDevice();
            Asset asset = CreateTestAsset();
            Schema schema = new Schema
            {
                Name = "test-schema",
                Version = "1.0.0",
                Format = Format.JsonSchemaDraft07,
                SchemaType = SchemaType.MessageSchema,
            };

            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAzureDeviceRegistryClientWrapper mockAdrClientWrapper = new MockAzureDeviceRegistryClientWrapper();
            IMessageSchemaProvider messageSchemaProviderFactory = new MockMessageSchemaProvider();
            Mock<ILogger<PollingTelemetryConnectorWorker>> mockLogger = new Mock<ILogger<PollingTelemetryConnectorWorker>>();
            ConnectorWorker worker = new ConnectorWorker(new ApplicationContext(),
                mockLogger.Object,
                mockMqttClient,
                messageSchemaProviderFactory,
                new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

            // Act
            AssetEvent assetEvent = CreateTestAssetEvent();
            var result = worker.ConstructCloudEventHeadersForEvent(
                device,
                TestDeviceName,
                TestInboundEndpointName,
                asset,
                TestAssetName,
                TestEventGroupName,
                assetEvent,
                schema,
                protocolSpecificIdentifier: null);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Source);
            Assert.DoesNotContain(TestEndpointAddress, result.Source.ToString());
        }

        private static Device CreateTestDevice()
        {
            return new Device()
            {
                Uuid = TestDeviceUuid,
                Endpoints = new()
                {
                    Inbound = new()
                    {
                        {
                            TestInboundEndpointName,
                            new()
                            {
                                // This address should NOT be used as a fallback in CloudEvent source
                                Address = TestEndpointAddress,
                            }
                        }
                    }
                }
            };
        }

        private static Asset CreateTestAsset()
        {
            return new Asset()
            {
                Uuid = TestAssetUuid,
                DeviceRef = new()
                {
                    DeviceName = TestDeviceName,
                    EndpointName = TestInboundEndpointName,
                }
            };
        }

        private AssetEvent CreateTestAssetEvent()
        {
            return new AssetEvent()
            {
                Name = "test-event"
            };
        }

    }
}
