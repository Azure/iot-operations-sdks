﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Moq;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.AssetAndDeviceRegistry;

public class AdrServiceClientTests
{
    [Fact]
    public async Task AdrServiceClientThrowsIfAccessedWhenDisposed()
    {
        // Arrange
        Mock<IMqttPubSubClient> mqttClient = new();
        mqttClient.Setup(mock => mock.ClientId).Returns("ConnectorClientId");
        ApplicationContext applicationContext = new();
        AdrServiceClient client = new(applicationContext, mqttClient.Object);

        // Act - Dispose
        await client.DisposeAsync();

        // Assert - Methods should throw ObjectDisposedException
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.GetDeviceAsync("TestDevice_1_Name", "TestEndpointName"));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.SetNotificationPreferenceForDeviceUpdatesAsync("TestDevice_1_Name", "TestEndpointName", Services.AssetAndDeviceRegistry.Models.NotificationPreference.On));
    }

    [Fact]
    public async Task AdrServiceClientThrowsIfCancellationRequested()
    {
        // Arrange
        Mock<IMqttPubSubClient> mqttClient = new();
        mqttClient.Setup(mock => mock.ClientId).Returns("ConnectorClientId");
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient.Object);

        CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        // Assert - Methods should throw TaskCanceledException
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await client.GetDeviceAsync("TestDevice_1_Name", "TestEndpointName", cancellationToken: cts.Token));
        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await client.SetNotificationPreferenceForDeviceUpdatesAsync("TestDevice_1_Name", "TestEndpointName", Services.AssetAndDeviceRegistry.Models.NotificationPreference.On, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task AdrServiceClientRetryWorks()
    {
        // Arrange
        ApplicationContext applicationContext = new();
        string connectorClientId = Guid.NewGuid().ToString();
        Mock<IDeviceDiscoveryServiceClientStub> mockDeviceDiscoveryService = new Mock<IDeviceDiscoveryServiceClientStub>();
        Mock<IAdrBaseServiceClientStub> mockBaseServiceClient = new Mock<IAdrBaseServiceClientStub>();

        await using AdrServiceClient client = new(applicationContext, connectorClientId, mockBaseServiceClient.Object, mockDeviceDiscoveryService.Object);

        int attemptCount = 0;
        mockBaseServiceClient.Setup(mock =>
            mock.CreateOrUpdateDiscoveredAssetAsync(
                It.IsAny<CreateOrUpdateDiscoveredAssetRequestPayload>(),
                It.IsAny<CommandRequestMetadata>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (attemptCount++ == 0)
                {
                    throw new Services.AssetAndDeviceRegistry.AdrBaseService.AkriServiceErrorException(
                        new Services.AssetAndDeviceRegistry.AdrBaseService.AkriServiceError()
                        {
                            Code = CodeSchema.BadRequest,
                            Message = "mock error",
                            Timestamp = new DateTime()
                        });
                }

                return new RpcCallAsync<Services.AssetAndDeviceRegistry.AdrBaseService.CreateOrUpdateDiscoveredAssetResponsePayload>(Task.FromResult(new ExtendedResponse<Services.AssetAndDeviceRegistry.AdrBaseService.CreateOrUpdateDiscoveredAssetResponsePayload>() { Response = new() { DiscoveredAssetResponse = new() {DiscoveryId = "discoveryId", Version = 123 } } }), new Guid());
            });

        await client.CreateOrUpdateDiscoveredAssetAsync(
            "someDeviceName",
            "someInboundEndpointName",
            new CreateOrUpdateDiscoveredAssetRequest()
            {
                DiscoveredAsset = new()
                {
                    DeviceRef = new()
                    {
                        DeviceName = "someDeviceName",
                        EndpointName = "someInboundEndpointName"
                    },
                },
                DiscoveredAssetName = "someDiscoveredAssetName"
            });
    }

}
