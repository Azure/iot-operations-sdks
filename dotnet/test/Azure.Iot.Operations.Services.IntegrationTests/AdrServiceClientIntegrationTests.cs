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
        var device = await client.GetDeviceAsync(TestDeviceName, TestEndpointName);

        // Assert
        _output.WriteLine($"Device: {device?.Name}");
        Assert.NotNull(device);
        Assert.Equal(TestDeviceName, device.Name);
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
    public async Task CanObserveAndUnobserveDeviceEndpointUpdatesAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        bool notificationReceived = false;
        client.OnReceiveDeviceUpdateEventTelemetry += (source, device) =>
        {
            _output.WriteLine($"Device update received from: {source}");
            notificationReceived = true;
            return Task.CompletedTask;
        };

        // Act - Observe
        var observeResponse = await client.ObserveDeviceEndpointUpdatesAsync(TestDeviceName, TestEndpointName);

        // Trigger an update so we can observe it
        var status = new DeviceStatus
        {
            Health = DeviceHealth.Good,
            LastActivityTime = DateTimeOffset.UtcNow,
            StatusMessage = "Update to trigger notification"
        };
        await client.UpdateDeviceStatusAsync(TestDeviceName, TestEndpointName, status);

        // Wait a short time for the notification to arrive
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Act - Unobserve
        var unobserveResponse = await client.UnobserveDeviceEndpointUpdatesAsync(TestDeviceName, TestEndpointName);

        // Assert
        Assert.NotNull(observeResponse);
        Assert.Equal(NotificationState.On, observeResponse.NotificationState);
        Assert.NotNull(unobserveResponse);
        Assert.Equal(NotificationState.Off, unobserveResponse.NotificationState);
        Assert.True(notificationReceived, "Expected to receive device update notification");
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
        var request = new UpdateAssetStatusRequest
        {
            AssetName = TestAssetName,
            Status = new AssetStatus
            {
                Health = AssetHealth.Good,
                LastActivityTime = DateTimeOffset.UtcNow,
                StatusMessage = "Test asset status update"
            }
        };

        // Act
        var updatedAsset = await client.UpdateAssetStatusAsync(TestDeviceName, TestEndpointName, request);

        // Assert
        _output.WriteLine($"Updated asset: {updatedAsset.Name}, Health: {updatedAsset.Status?.Health}");
        Assert.NotNull(updatedAsset);
        Assert.Equal(TestAssetName, updatedAsset.Name);
        Assert.Equal(AssetHealth.Good, updatedAsset.Status?.Health);
        Assert.Equal("Test asset status update", updatedAsset.Status?.StatusMessage);
    }

    [Fact]
    public async Task CanObserveAndUnobserveAssetUpdatesAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        bool notificationReceived = false;
        client.OnReceiveAssetUpdateEventTelemetry += (source, asset) =>
        {
            _output.WriteLine($"Asset update received from: {source}");
            notificationReceived = true;
            return Task.CompletedTask;
        };

        // Act - Observe
        var observeResponse = await client.ObserveAssetUpdatesAsync(TestDeviceName, TestEndpointName, TestAssetName);

        // Trigger an update so we can observe it
        var updateRequest = new UpdateAssetStatusRequest
        {
            AssetName = TestAssetName,
            Status = new AssetStatus
            {
                Health = AssetHealth.Good,
                LastActivityTime = DateTimeOffset.UtcNow,
                StatusMessage = "Update to trigger asset notification"
            }
        };
        await client.UpdateAssetStatusAsync(TestDeviceName, TestEndpointName, updateRequest);

        // Wait a short time for the notification to arrive
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Act - Unobserve
        var unobserveResponse = await client.UnobserveAssetUpdatesAsync(TestDeviceName, TestEndpointName, TestAssetName);

        // Assert
        Assert.NotNull(observeResponse);
        Assert.Equal(NotificationState.On, observeResponse.NotificationState);
        Assert.NotNull(unobserveResponse);
        Assert.Equal(NotificationState.Off, unobserveResponse.NotificationState);
        Assert.True(notificationReceived, "Expected to receive asset update notification");
    }

    [Fact]
    public async Task CanCreateDetectedAssetAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);
        var request = new CreateDetectedAssetRequest
        {
            DetectedAsset = new DetectedAsset
            {
                AssetName = $"detected-asset-{Guid.NewGuid():N}",
                Description = "Test detected asset",
                Properties = new Dictionary<string, object>
                {
                    { "property1", "value1" },
                    { "property2", 42 }
                }
            }
        };

        // Act
        var response = await client.CreateDetectedAssetAsync(TestDeviceName, TestEndpointName, request);

        // Assert
        _output.WriteLine($"Created detected asset: {response?.CreatedAsset?.Name}");
        Assert.NotNull(response);
        Assert.NotNull(response.CreatedAsset);
        Assert.Equal(request.DetectedAsset.AssetName, response.CreatedAsset.Name);
        Assert.Equal(request.DetectedAsset.Description, response.CreatedAsset.Description);
    }

    [Fact]
    public async Task CanCreateDiscoveredAssetEndpointProfileAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);
        var profileId = $"test-profile-{Guid.NewGuid():N}";
        var request = new CreateDiscoveredAssetEndpointProfileRequest
        {
            EndpointProfileType = "opc-ua",
            DiscoveredAssetEndpointProfile = new DiscoveredAssetEndpointProfile
            {
                Id = profileId,
                Description = "Test discovered endpoint profile",
                Properties = new Dictionary<string, object>
                {
                    { "endpointUrl", "opc.tcp://test.server:4840" },
                    { "securityPolicy", "None" }
                }
            }
        };

        // Act
        var response = await client.CreateDiscoveredAssetEndpointProfileAsync(request);

        // Assert
        _output.WriteLine($"Created endpoint profile: {response?.CreatedProfile?.Id}");
        Assert.NotNull(response);
        Assert.NotNull(response.CreatedProfile);
        Assert.Equal(profileId, response.CreatedProfile.Id);
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
}
