// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.ConnectorConfigurations;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    /// <summary>
    /// Tests for the SDK-owned baseline <see cref="DeviceStatus"/> that the base
    /// <see cref="ConnectorWorker"/> publishes on the device lifecycle. Publishing the device
    /// baseline is a device-lifecycle concern, independent of which asset components (datasets,
    /// events, management actions) exist: when the connector supplies no
    /// <see cref="ConnectorWorker.WhileDeviceIsAvailable"/> callback, the SDK owns the baseline so
    /// <see cref="DeviceStatus.Config"/> never stays null; when a callback IS supplied the connector
    /// reports device status from there and the SDK must NOT double-write.
    /// </summary>
    // These tests rely on environment variables which may interfere with other similar tests.
    [Collection("Environment Variable Sequential")]
    public sealed class ConnectorWorkerDeviceStatusTests
    {
        public ConnectorWorkerDeviceStatusTests()
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

        [Fact]
        public async Task DeviceCreated_NoDeviceCallback_PublishesHealthyBaselineDeviceStatus()
        {
            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAzureDeviceRegistryClientWrapper mockAdrClientWrapper = new MockAzureDeviceRegistryClientWrapper();
            IMessageSchemaProvider messageSchemaProvider = new MockMessageSchemaProvider();
            Mock<ILogger<ConnectorWorker>> mockLogger = new Mock<ILogger<ConnectorWorker>>();

            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();

            // The connector is the sole authoritative writer; start from an empty status as ADR would
            // return before the connector has reported anything.
            mockAdrClientWrapper.mockClientWrapper
                .Setup(c => c.GetDeviceStatusAsync(deviceName, inboundEndpointName, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeviceStatus());

            TaskCompletionSource<DeviceStatus> publishedStatusTcs = new();
            mockAdrClientWrapper.mockClientWrapper
                .Setup(c => c.UpdateDeviceStatusAsync(deviceName, inboundEndpointName, It.IsAny<DeviceStatus>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, DeviceStatus, TimeSpan?, CancellationToken>((_, _, status, _, _) => publishedStatusTcs.TrySetResult(status))
                .ReturnsAsync((string _, string _, DeviceStatus status, TimeSpan? _, CancellationToken _) => status);

            // Base ConnectorWorker with no WhileDeviceIsAvailable callback => SDK owns the baseline.
            ConnectorWorker worker = new ConnectorWorker(
                new ApplicationContext(),
                mockLogger.Object,
                mockMqttClient,
                messageSchemaProvider,
                new MockAdrClientFactory(mockAdrClientWrapper));
            _ = worker.StartAsync(CancellationToken.None);

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

            DeviceStatus published = await publishedStatusTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));

            // Config baseline is populated and healthy.
            Assert.NotNull(published.Config);
            Assert.Null(published.Config.Error);
            Assert.NotNull(published.Config.LastTransitionTime);

            // The inbound endpoint has a healthy entry.
            Assert.NotNull(published.Endpoints);
            Assert.NotNull(published.Endpoints.Inbound);
            Assert.True(published.Endpoints.Inbound.ContainsKey(inboundEndpointName));
            Assert.Null(published.Endpoints.Inbound[inboundEndpointName].Error);

            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }

        [Fact]
        public async Task DeviceCreated_WithDeviceCallback_DoesNotPublishBaselineDeviceStatus()
        {
            MockMqttClient mockMqttClient = new MockMqttClient();
            MockAzureDeviceRegistryClientWrapper mockAdrClientWrapper = new MockAzureDeviceRegistryClientWrapper();
            IMessageSchemaProvider messageSchemaProvider = new MockMessageSchemaProvider();
            Mock<ILogger<ConnectorWorker>> mockLogger = new Mock<ILogger<ConnectorWorker>>();

            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();

            ConnectorWorker worker = new ConnectorWorker(
                new ApplicationContext(),
                mockLogger.Object,
                mockMqttClient,
                messageSchemaProvider,
                new MockAdrClientFactory(mockAdrClientWrapper));

            // The connector reports device status from its own callback, so the SDK must not write a
            // baseline (no double-write).
            TaskCompletionSource deviceCallbackInvokedTcs = new();
            worker.WhileDeviceIsAvailable = (args, cancellationToken) =>
            {
                deviceCallbackInvokedTcs.TrySetResult();
                return Task.CompletedTask;
            };

            _ = worker.StartAsync(CancellationToken.None);

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

            // Wait until the device is processed (the user callback ran), then give any erroneous
            // background publish a chance to run before asserting it never happened.
            await deviceCallbackInvokedTcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Task.Delay(TimeSpan.FromMilliseconds(250));

            mockAdrClientWrapper.mockClientWrapper.Verify(
                c => c.GetDeviceStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
                Times.Never);
            mockAdrClientWrapper.mockClientWrapper.Verify(
                c => c.UpdateDeviceStatusAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DeviceStatus>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
                Times.Never);

            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }
    }
}
