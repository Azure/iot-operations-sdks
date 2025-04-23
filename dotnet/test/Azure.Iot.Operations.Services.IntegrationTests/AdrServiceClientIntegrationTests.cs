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
    private const string TestDeviceName = "my-thermostat";
    private const string TestEndpointName = "my-rest-endpoint";
    private const string TestAssetName = "my-rest-thermostat-asset";

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
        var status = CreateDeviceStatus(DateTime.UtcNow);
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
        var status = CreateDeviceStatus(DateTime.UtcNow);
        await client.UpdateDeviceStatusAsync(TestDeviceName, TestEndpointName, status);

        // Wait a short time for the notification to arrive
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Act - Unobserve
        var unobserveResponse = await client.UnobserveDeviceEndpointUpdatesAsync(TestDeviceName, TestEndpointName);

        status = CreateDeviceStatus(DateTime.UtcNow);
        await client.UpdateDeviceStatusAsync(TestDeviceName, TestEndpointName, status);

        // Wait a short time for the notification to arrive
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(1, notificationReceivedCount);
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

        UpdateAssetStatusRequest request = CreateUpdateAssetStatusRequest(DateTime.UtcNow);

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
        UpdateAssetStatusRequest updateRequest = CreateUpdateAssetStatusRequest(DateTime.Now);
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
        UpdateAssetStatusRequest updateRequest = CreateUpdateAssetStatusRequest(DateTime.Now);
        await client.UpdateAssetStatusAsync(TestDeviceName, TestEndpointName, updateRequest);

        // Wait a short time for the notification to arrive
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Act - Unobserve
        var unobserveResponse = await client.UnobserveAssetUpdatesAsync(TestDeviceName, TestEndpointName, TestAssetName);

        // Trigger an update so we can observe it
        updateRequest = CreateUpdateAssetStatusRequest(DateTime.Now);
        await client.UpdateAssetStatusAsync(TestDeviceName, TestEndpointName, updateRequest);

        // Wait a short time for the notification to arrive
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(1, notificationReceivedCount);
    }

    [Fact(Skip = "Requires ADR service changes")]
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

    [Fact]
    public async Task AssetEventStreamDataValidation()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        var receivedEvents = new List<Asset>();
        var eventReceived = new TaskCompletionSource<bool>();

        // Set up event handler to capture and validate events
        client.OnReceiveAssetUpdateEventTelemetry += (_, asset) =>
        {
            if (asset != null)
            {
                _output.WriteLine($"Received asset event: {asset.Name}");
                receivedEvents.Add(asset);

                // Verify events data is present and correctly structured
                if (asset.Status?.Events != null && asset.Status.Events.Count > 0)
                {
                    _output.WriteLine($"Events count: {asset.Status.Events.Count}");
                    foreach (var evt in asset.Status.Events)
                    {
                        _output.WriteLine($"Event: {evt.Name}, Schema: {evt.MessageSchemaReference?.SchemaName}");
                    }
                    eventReceived.TrySetResult(true);
                }
            }
            return Task.CompletedTask;
        };

        // Start observing asset updates
        await client.ObserveAssetUpdatesAsync(TestDeviceName, TestEndpointName, TestAssetName);

        // Act - Update asset with event data to trigger notification
        var updateRequest = CreateUpdateAssetStatusRequest(DateTime.UtcNow);
        updateRequest.AssetStatus.Events = new List<AssetDatasetEventStreamStatus>
        {
            new AssetDatasetEventStreamStatus
            {
                Name = "temperature-event",
                MessageSchemaReference = new MessageSchemaReference
                {
                    SchemaName = "temperature-schema",
                    SchemaRegistryNamespace = "test-namespace",
                    SchemaVersion = "1.0"
                }
            }
        };

        var updatedAsset = await client.UpdateAssetStatusAsync(TestDeviceName, TestEndpointName, updateRequest);

        // Wait for event to be received or timeout
        var receivedEventsTask = await Task.WhenAny(
            eventReceived.Task,
            Task.Delay(TimeSpan.FromSeconds(5)));

        // Cleanup
        await client.UnobserveAssetUpdatesAsync(TestDeviceName, TestEndpointName, TestAssetName);

        // Assert
        Assert.True(receivedEventsTask.IsCompleted && !receivedEventsTask.IsFaulted,
            "Did not receive asset event update within timeout");
        Assert.NotEmpty(receivedEvents);

        // Validate event content
        var latestEvent = receivedEvents[^1];
        Assert.NotNull(latestEvent.Status?.Events);
        Assert.Contains(latestEvent.Status.Events, e => e.Name == "temperature-event");

        var eventData = latestEvent.Status.Events.Find(e => e.Name == "temperature-event");
        Assert.NotNull(eventData?.MessageSchemaReference);
        Assert.Equal("temperature-schema", eventData.MessageSchemaReference.SchemaName);
        Assert.Equal("test-namespace", eventData.MessageSchemaReference.SchemaRegistryNamespace);
        Assert.Equal("1.0", eventData.MessageSchemaReference.SchemaVersion);
    }

    [Fact]
    public async Task MultipleEventStreamsAndErrorHandling()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        // Start observing asset updates
        var observeResponse = await client.ObserveAssetUpdatesAsync(
            TestDeviceName, TestEndpointName, TestAssetName);
        Assert.Equal(NotificationResponse.Accepted, observeResponse);

        // Act - Update asset with multiple event streams including an error case
        var updateRequest = CreateUpdateAssetStatusRequest(DateTime.UtcNow);
        updateRequest.AssetStatus.Events = new List<AssetDatasetEventStreamStatus>
        {
            new AssetDatasetEventStreamStatus
            {
                Name = "valid-event",
                MessageSchemaReference = new MessageSchemaReference
                {
                    SchemaName = "valid-schema",
                    SchemaRegistryNamespace = "test-namespace",
                    SchemaVersion = "1.0"
                }
            },
            new AssetDatasetEventStreamStatus
            {
                Name = "error-event",
                Error = new ConfigError
                {
                    Code = "event-error-code",
                    Message = "Event stream configuration error",
                    Details = new List<DetailsSchemaElement>
                    {
                        new()
                        {
                            Code = "validation-error",
                            Message = "Schema validation failed",
                            CorrelationId = Guid.NewGuid().ToString()
                        }
                    }
                }
            }
        };

        var updatedAsset = await client.UpdateAssetStatusAsync(
            TestDeviceName, TestEndpointName, updateRequest);

        // Get asset to verify state after update
        var asset = await client.GetAssetAsync(
            TestDeviceName,
            TestEndpointName,
            new GetAssetRequest { AssetName = TestAssetName });

        // Cleanup
        await client.UnobserveAssetUpdatesAsync(TestDeviceName, TestEndpointName, TestAssetName);

        // Assert
        Assert.NotNull(updatedAsset);
        Assert.NotNull(asset);
        Assert.NotNull(asset.Status?.Events);
        Assert.Equal(2, asset.Status.Events.Count);

        // Verify valid event stream
        var validEvent = asset.Status.Events.Find(e => e.Name == "valid-event");
        Assert.NotNull(validEvent);
        Assert.NotNull(validEvent.MessageSchemaReference);
        Assert.Equal("valid-schema", validEvent.MessageSchemaReference.SchemaName);

        // Verify error event stream
        var errorEvent = asset.Status.Events.Find(e => e.Name == "error-event");
        Assert.NotNull(errorEvent);
        Assert.NotNull(errorEvent.Error);
        Assert.Equal("event-error-code", errorEvent.Error.Code);
        Assert.Equal("Event stream configuration error", errorEvent.Error.Message);
        Assert.NotNull(errorEvent.Error.Details);
        Assert.Single(errorEvent.Error.Details);
        Assert.Equal("validation-error", errorEvent.Error.Details[0].Code);
    }

    private CreateDetectedAssetRequest CreateCreateDetectedAssetRequest()
    {
        return new CreateDetectedAssetRequest
        {
            AssetName = TestAssetName,
            AssetEndpointProfileRef = TestEndpointName,
        };
    }

    private static DeviceStatus CreateDeviceStatus(DateTime timeStamp)
    {
        return new DeviceStatus
        {
            Config = new DeviceStatusConfig
            {
                Error = null,
                LastTransitionTime = timeStamp.ToString("o"),
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

    private UpdateAssetStatusRequest CreateUpdateAssetStatusRequest(DateTime timeStamp)
    {
        return new UpdateAssetStatusRequest
        {
            AssetName = TestAssetName,
            AssetStatus = new AssetStatus
            {
                Config = new AssetConfigStatus
                {
                    Error = null,
                    LastTransitionTime = timeStamp.ToString("o"),
                    Version = 1
                }
            }
        };
    }

}
