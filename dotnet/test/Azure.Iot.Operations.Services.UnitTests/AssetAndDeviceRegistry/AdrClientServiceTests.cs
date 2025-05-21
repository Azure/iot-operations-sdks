// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Moq;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.AssetAndDeviceRegistry;

public class AdrClientServiceTests
{
    [Fact]
    public async Task AdrServiceClientThrowsIfAccessedWhenDisposed()
    {
        // Arrange
        Mock<IMqttPubSubClient> mqttClient = new();
        ApplicationContext applicationContext = new();
        AdrServiceClient client = new(applicationContext, mqttClient.Object, "ConnectorClientId");

        // Act - Dispose
        await client.DisposeAsync();

        // Assert - Methods should throw ObjectDisposedException
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.GetDeviceAsync("TestDevice_1_Name", "TestEndpointName"));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.ObserveDeviceEndpointUpdatesAsync("TestDevice_1_Name", "TestEndpointName"));
    }

    [Fact]
    public async Task AdrServiceClientThrowsIfCancellationRequested()
    {
        // Arrange
        Mock<IMqttPubSubClient> mqttClient = new();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient.Object, "ConnectorClientId");

        CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        // Assert - Methods should throw OperationCanceledException
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await client.GetDeviceAsync("TestDevice_1_Name", "TestEndpointName", cancellationToken: cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await client.ObserveDeviceEndpointUpdatesAsync("TestDevice_1_Name", "TestEndpointName", cancellationToken: cts.Token));
    }

}
