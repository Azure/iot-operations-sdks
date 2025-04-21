// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.IntegrationTests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mqtt.Session;
using Protocol;
using AssetAndDeviceRegistry;
using AssetAndDeviceRegistry.Models;
using IntegrationTest;
using Xunit;
using Xunit.Abstractions;

[Trait("Category", "ADR")]
public class AdrServiceClientIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private const string ConnectorClientId = "test-connector-client";
    private const string TestDeviceName = "test-device";
    private const string TestEndpointName = "test-endpoint";
    private const string TestAssetName = "test-asset";

    public AdrServiceClientIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CanGetDeviceAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        // Act
        var device = await client.GetDeviceAsync("my-thermostat", "my-rest-endpoint");

        // Assert
        _output.WriteLine($"Device: {device?.Name}");
        Assert.NotNull(device);
        Assert.Equal("my-thermostat", device.Name);
    }

    [Fact]
    public async Task CanUpdateDeviceStatusAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        var status = new DeviceStatus
        {
            Config = new DeviceStatusConfig
            {
                Error = null,
                LastTransitionTime = "2023-10-01T00:00:00Z",
                Version = 1
            },
            Endpoints = new DeviceStatusEndpoint
            {
                Inbound = new Dictionary<string, DeviceStatusInboundEndpointSchemaMapValue>
                {
                    { TestEndpointName, new DeviceStatusInboundEndpointSchemaMapValue() }
                }
            }
        };

        // Act
        Device updatedDevice = await client.UpdateDeviceStatusAsync(TestDeviceName, TestEndpointName, status);

        // Assert
        Assert.NotNull(updatedDevice);
        Assert.Equal(TestDeviceName, updatedDevice.Name);
        _output.WriteLine($"Updated device: {updatedDevice.Name}");
    }

    [Fact]
    public async Task TriggerDeviceTelemetryEventWhenObservedAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        int notificationReceivedCount = 0;
        client.OnReceiveDeviceUpdateEventTelemetry += (source, device) =>
        {
            _output.WriteLine($"Device update received from: {source}");
            notificationReceivedCount++;
            return Task.CompletedTask;
        };

        // Act - Observe
        var observeResponse = await client.ObserveDeviceEndpointUpdatesAsync(TestDeviceName, TestEndpointName);

        // Trigger an update so we can observe it
        var status = CreateDeviceStatus();
        await client.UpdateDeviceStatusAsync(TestDeviceName, TestEndpointName, status);

        // Wait a short time for the notification to arrive
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(1, notificationReceivedCount);
    }

    [Fact]
    public async Task DoNotTriggerTelemetryEventAfterUnobserveDeviceAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        int notificationReceivedCount = 0;
        client.OnReceiveDeviceUpdateEventTelemetry += (source, device) =>
        {
            _output.WriteLine($"Device update received from: {source}");
            notificationReceivedCount++;
            return Task.CompletedTask;
        };

        // Act - Observe
        var observeResponse = await client.ObserveDeviceEndpointUpdatesAsync(TestDeviceName, TestEndpointName);

        // Trigger an update so we can observe it
        var status = CreateDeviceStatus();
        await client.UpdateDeviceStatusAsync(TestDeviceName, TestEndpointName, status);

        // Wait a short time for the notification to arrive
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Act - Unobserve
        var unobserveResponse = await client.UnobserveDeviceEndpointUpdatesAsync(TestDeviceName, TestEndpointName);

        await client.UpdateDeviceStatusAsync(TestDeviceName, TestEndpointName, status);

        // Wait a short time for the notification to arrive
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(1, notificationReceivedCount);
    }

    private static DeviceStatus CreateDeviceStatus()
    {
        return new DeviceStatus
        {
            Config = new DeviceStatusConfig
            {
                Error = null,
                LastTransitionTime = "2023-10-01T12:00:00Z",
                Version = 2
            },
            Endpoints = new DeviceStatusEndpoint
            {
                Inbound = new Dictionary<string, DeviceStatusInboundEndpointSchemaMapValue>
                {
                    { TestEndpointName, new DeviceStatusInboundEndpointSchemaMapValue() }
                }
            }
        };
    }

    [Fact]
    public async Task CanGetAssetAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);
        var request = new GetAssetRequest
        {
            AssetName = TestAssetName
        };

        // Act
        var asset = await client.GetAssetAsync(TestDeviceName, TestEndpointName, request);

        // Assert
        _output.WriteLine($"Asset: {asset?.Name}");
        Assert.NotNull(asset);
        Assert.Equal(TestAssetName, asset.Name);
    }

    [Fact]
    public async Task CanUpdateAssetStatusAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        UpdateAssetStatusRequest request = CreateUpdateAssetStatusRequest();

        // Act
        var updatedAsset = await client.UpdateAssetStatusAsync(TestDeviceName, TestEndpointName, request);

        // Assert
        Assert.NotNull(updatedAsset);
        Assert.Equal(TestAssetName, updatedAsset.Name);
    }

    [Fact]
    public async Task TriggerAssetTelemetryEventWhenObservedAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        int notificationReceivedCount = 0;
        client.OnReceiveAssetUpdateEventTelemetry += (source, asset) =>
        {
            _output.WriteLine($"Asset update received from: {source}");
            notificationReceivedCount++;
            return Task.CompletedTask;
        };

        // Act - Observe
        var observeResponse = await client.ObserveAssetUpdatesAsync(TestDeviceName, TestEndpointName, TestAssetName);

        // Trigger an update so we can observe it
        UpdateAssetStatusRequest updateRequest = CreateUpdateAssetStatusRequest();
        await client.UpdateAssetStatusAsync(TestDeviceName, TestEndpointName, updateRequest);

        // Wait a short time for the notification to arrive
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(1, notificationReceivedCount);
    }

    [Fact]
    public async Task DoNotTriggerTelemetryEventAfterUnobserveAssetAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        int notificationReceivedCount = 0;
        client.OnReceiveAssetUpdateEventTelemetry += (source, asset) =>
        {
            _output.WriteLine($"Asset update received from: {source}");
            notificationReceivedCount++;
            return Task.CompletedTask;
        };

        // Act - Observe
        var observeResponse = await client.ObserveAssetUpdatesAsync(TestDeviceName, TestEndpointName, TestAssetName);

        // Trigger an update so we can observe it
        UpdateAssetStatusRequest updateRequest = CreateUpdateAssetStatusRequest();
        await client.UpdateAssetStatusAsync(TestDeviceName, TestEndpointName, updateRequest);

        // Wait a short time for the notification to arrive
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Act - Unobserve
        var unobserveResponse = await client.UnobserveAssetUpdatesAsync(TestDeviceName, TestEndpointName, TestAssetName);

        // Assert
        Assert.Equal(1, notificationReceivedCount);
    }

    [Fact]
    public async Task CanCreateDetectedAssetAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        CreateDetectedAssetRequest request = CreateCreateDetectedAssetRequest();

        // Act
        var response = await client.CreateDetectedAssetAsync(TestDeviceName, TestEndpointName, request);

        // Assert
    }

    [Fact]
    public async Task AdrServiceClientThrowsIfAccessedWhenDisposed()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        // Act - Dispose
        await client.DisposeAsync();

        // Assert - Methods should throw ObjectDisposedException
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.GetDeviceAsync(TestDeviceName, TestEndpointName));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await client.ObserveDeviceEndpointUpdatesAsync(TestDeviceName, TestEndpointName));
    }

    [Fact]
    public async Task AdrServiceClientThrowsIfCancellationRequested()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        // Assert - Methods should throw OperationCanceledException
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await client.GetDeviceAsync(TestDeviceName, TestEndpointName, cancellationToken: cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await client.ObserveDeviceEndpointUpdatesAsync(TestDeviceName, TestEndpointName, cancellationToken: cts.Token));
    }

    private CreateDetectedAssetRequest CreateCreateDetectedAssetRequest()
    {
        return new CreateDetectedAssetRequest
        {
            AssetName = TestAssetName,
            AssetEndpointProfileRef = "test-asset-endpoint-profile",
        };
    }

    private UpdateAssetStatusRequest CreateUpdateAssetStatusRequest()
    {
        return new UpdateAssetStatusRequest
        {
            AssetName = TestAssetName,
            AssetStatus = new AssetStatus
            {
                Config = new AssetConfigStatus
                {
                    Error = null,
                    LastTransitionTime = "2023-10-01T12:00:00Z",
                    Version = 1
                }
            }
        };
    }

}
