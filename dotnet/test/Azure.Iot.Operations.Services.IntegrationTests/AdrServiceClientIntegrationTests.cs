﻿// Copyright (c) Microsoft Corporation.
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
    private const string TestDevice_1_Name = "my-thermostat";
    private const string TestDevice_2_Name = "test-thermostat";
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
        var device = await client.GetDeviceAsync(TestDevice_1_Name, "my-rest-endpoint");

        // Assert
        _output.WriteLine($"Device: {TestDevice_1_Name}");
        Assert.NotNull(device);
        Assert.NotNull(device.Endpoints);
        Assert.NotNull(device.Endpoints.Inbound);
        Assert.Single(device.Endpoints.Inbound.Keys);
        Assert.True(device.Endpoints.Inbound.ContainsKey("my-rest-thermostat-endpoint-name"));
    }

    [Fact]
    public async Task CanGetDeviceStatusAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        // Act
        var deviceStatus = await client.GetDeviceStatusAsync(TestDevice_1_Name, "my-rest-endpoint");

        // Assert
        _output.WriteLine($"Device: {TestDevice_1_Name}");
        Assert.NotNull(deviceStatus.Endpoints);
        Assert.NotNull(deviceStatus.Endpoints.Inbound);
        Assert.Single(deviceStatus.Endpoints.Inbound);
    }

    [Fact]
    public async Task CanGetAssetStatusAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        // Act
        var assetStatus = await client.GetAssetStatusAsync(TestDevice_1_Name, TestEndpointName, TestAssetName);

        // Assert
        _output.WriteLine($"Device: {TestDevice_1_Name}");
        Assert.NotNull(assetStatus.Datasets);
        Assert.Single(assetStatus.Datasets);
    }

    [Fact]
    public async Task GetDeviceThrowsAkriServiceErrorExceptionWhenDeviceNotFoundAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AkriServiceErrorException>(
            () => client.GetDeviceAsync("non-existent-device", "my-rest-endpoint"));

        _output.WriteLine($"Expected exception: {exception.Message}");
        Assert.NotNull(exception.AkriServiceError);
        Assert.Equal(Code.KubeError, exception.AkriServiceError.Code);
    }

    [Fact (Skip = "service doesn't support Update APIs yet")]
    public async Task CanUpdateDeviceStatusAsync()
    {
        // Arrange
        var expectedTime = DateTime.Parse("2023-10-01T00:00:00Z");
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        var status = new DeviceStatus
        {
            Config = new DeviceStatusConfig
            {
                Error = null,
                LastTransitionTime = expectedTime,
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
        DeviceStatus updatedDevice = await client.UpdateDeviceStatusAsync(TestDevice_1_Name, TestEndpointName, status);

        // Assert
        Assert.NotNull(updatedDevice);
        Assert.NotNull(updatedDevice.Config);
        Assert.Equal(expectedTime, updatedDevice.Config.LastTransitionTime);
    }

    [Fact(Skip = "service doesn't support Update APIs yet")]
    public async Task TriggerDeviceTelemetryEventWhenObservedAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        var eventReceived = new TaskCompletionSource<bool>();
        client.OnReceiveDeviceUpdateEventTelemetry += (source, _) =>
        {
            _output.WriteLine($"Device update received from: {source}");
            eventReceived.TrySetResult(true);
            return Task.CompletedTask;
        };

        // Act - Observe
        await client.SetNotificationPreferenceForDeviceUpdatesAsync(TestDevice_1_Name, TestEndpointName, NotificationPreference.On);

        // Trigger an update so we can observe it
        var status = CreateDeviceStatus(DateTime.UtcNow);
        await client.UpdateDeviceStatusAsync(TestDevice_1_Name, TestEndpointName, status);

        // Wait for the notification to arrive
        try
        {
            await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Did not receive device update event within timeout");
        }
    }

    [Fact(Skip = "service doesn't support Update APIs yet")]
    public async Task DoNotTriggerTelemetryEventAfterUnobserveDeviceAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        var firstEventReceived = new TaskCompletionSource<bool>();
        var secondEventReceived = new TaskCompletionSource<bool>();
        var eventCounter = 0;

        client.OnReceiveDeviceUpdateEventTelemetry += (source, _) =>
        {
            _output.WriteLine($"Device update received from: {source}");
            eventCounter++;

            if (eventCounter == 1)
            {
                firstEventReceived.TrySetResult(true);
            }
            else
            {
                secondEventReceived.TrySetResult(true);
            }

            return Task.CompletedTask;
        };

        // Act - Observe
        await client.SetNotificationPreferenceForDeviceUpdatesAsync(TestDevice_1_Name, TestEndpointName, NotificationPreference.On);

        // Trigger an update so we can observe it
        var status = CreateDeviceStatus(DateTime.UtcNow);
        await client.UpdateDeviceStatusAsync(TestDevice_1_Name, TestEndpointName, status);

        // Wait for the first notification to arrive
        try
        {
            await firstEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Did not receive first device update event within timeout");
        }

        // Act - Unobserve
        await client.SetNotificationPreferenceForDeviceUpdatesAsync(TestDevice_1_Name, TestEndpointName, NotificationPreference.Off);

        status = CreateDeviceStatus(DateTime.UtcNow);
        await client.UpdateDeviceStatusAsync(TestDevice_1_Name, TestEndpointName, status);

        // Wait to see if we get another notification (which we shouldn't)
        bool receivedUnexpectedNotification = false;
        try
        {
            await secondEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            receivedUnexpectedNotification = true;
        }
        catch (TimeoutException)
        {
        }

        // Assert
        Assert.False(receivedUnexpectedNotification, "Should not receive device update event after unobserving");
    }

    [Fact]
    public async Task CanGetAssetAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        // Act
        var asset = await client.GetAssetAsync(TestDevice_1_Name, TestEndpointName, TestAssetName);

        // Assert
        _output.WriteLine($"Asset: {TestAssetName}");
        Assert.NotNull(asset);
        Assert.NotNull(asset.Datasets);
        Assert.Single(asset.Datasets);
    }

    [Fact(Skip = "service doesn't support Update APIs yet")]
    public async Task CanUpdateAssetStatusAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        var expectedTime = DateTime.UtcNow;

        UpdateAssetStatusRequest request = CreateUpdateAssetStatusRequest(expectedTime);

        // Act
        var updatedAsset = await client.UpdateAssetStatusAsync(TestDevice_1_Name, TestEndpointName, request);

        // Assert
        Assert.NotNull(updatedAsset);
        Assert.NotNull(updatedAsset.Config);
        Assert.Equal(expectedTime, updatedAsset.Config.LastTransitionTime);
    }

    [Fact(Skip = "service doesn't support Update APIs yet")]
    public async Task TriggerAssetTelemetryEventWhenObservedAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        var eventReceived = new TaskCompletionSource<bool>();
        client.OnReceiveAssetUpdateEventTelemetry += (source, _) =>
        {
            _output.WriteLine($"Asset update received from: {source}");
            eventReceived.TrySetResult(true);
            return Task.CompletedTask;
        };

        // Act - Observe
        await client.SetNotificationPreferenceForAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName, NotificationPreference.On);

        // Trigger an update so we can observe it
        UpdateAssetStatusRequest updateRequest = CreateUpdateAssetStatusRequest(DateTime.Now);
        await client.UpdateAssetStatusAsync(TestDevice_1_Name, TestEndpointName, updateRequest);

        // Wait for the notification to arrive
        try
        {
            await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Did not receive asset update event within timeout");
        }
    }

    [Fact(Skip = "service doesn't support Update APIs yet")]
    public async Task DoNotTriggerTelemetryEventAfterUnobserveAssetAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        var firstEventReceived = new TaskCompletionSource<bool>();
        var secondEventReceived = new TaskCompletionSource<bool>();
        var eventCounter = 0;

        client.OnReceiveAssetUpdateEventTelemetry += (source, _) =>
        {
            _output.WriteLine($"Asset update received from: {source}");
            eventCounter++;

            if (eventCounter == 1)
            {
                firstEventReceived.TrySetResult(true);
            }
            else
            {
                secondEventReceived.TrySetResult(true);
            }

            return Task.CompletedTask;
        };

        // Act - Observe
        await client.SetNotificationPreferenceForAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName, NotificationPreference.On);

        // Trigger an update so we can observe it
        UpdateAssetStatusRequest updateRequest = CreateUpdateAssetStatusRequest(DateTime.Now);
        await client.UpdateAssetStatusAsync(TestDevice_1_Name, TestEndpointName, updateRequest);

        // Wait for the first notification to arrive
        try
        {
            await firstEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Did not receive first asset update event within timeout");
        }

        // Act - Unobserve
        await client.SetNotificationPreferenceForAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName, NotificationPreference.Off);

        // Trigger an update so we can observe it
        updateRequest = CreateUpdateAssetStatusRequest(DateTime.Now);
        await client.UpdateAssetStatusAsync(TestDevice_1_Name, TestEndpointName, updateRequest);

        // Wait to see if we get another notification (which we shouldn't)
        bool receivedUnexpectedNotification = false;
        try
        {
            await secondEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            receivedUnexpectedNotification = true;
        }
        catch (TimeoutException)
        {
        }

        // Assert
        Assert.False(receivedUnexpectedNotification, "Should not receive asset update event after unobserving");
    }

    [Fact]
    public async Task CanCreateOrUpdateDiscoveredAssetAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        var request = CreateCreateDetectedAssetRequest();

        // Act
        var result = await client.CreateOrUpdateDiscoveredAssetAsync(TestDevice_1_Name, TestEndpointName, request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.DiscoveredAssetResponse.DiscoveryId);
        _output.WriteLine($"Detected asset created with DiscoveryId: {result.DiscoveredAssetResponse.DiscoveryId}");
    }

    [Fact]
    public async Task CanCreateOrUpdateDiscoveredDeviceAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        var request = CreateCreateDiscoveredDeviceRequest();

        // Act
        var response = await client.CreateOrUpdateDiscoveredDeviceAsync(request, "my-rest-endpoint");

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.DiscoveredDeviceResponse.DiscoveryId);
        Assert.NotEmpty(response.DiscoveredDeviceResponse.DiscoveryId);
        Assert.Equal("test-discovered-device", response.DiscoveredDeviceResponse.DiscoveryId);
        _output.WriteLine($"Discovered device created with name: {response.DiscoveredDeviceResponse.DiscoveryId}");
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
        client.OnReceiveAssetUpdateEventTelemetry += (assetName, asset) =>
        {
            _output.WriteLine($"Received asset event: {assetName}");
            receivedEvents.Add(asset);

            // Verify events data is present and correctly structured
            if (asset.Events is { Count: > 0 })
            {
                _output.WriteLine($"Events count: {asset.Events.Count}");
                eventReceived.TrySetResult(true);
            }
            return Task.CompletedTask;
        };

        // Start observing asset updates
        await client.SetNotificationPreferenceForAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName, NotificationPreference.On);

        // Act - Update asset with event data to trigger notification
        var updateRequest = CreateUpdateAssetStatusRequest(DateTime.UtcNow);
        updateRequest.AssetStatus.Events =
        [
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
        ];

        await client.UpdateAssetStatusAsync(TestDevice_1_Name, TestEndpointName, updateRequest);

        // Wait for event to be received or timeout
        try
        {
            await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Did not receive asset event update within timeout");
        }

        // Cleanup
        await client.SetNotificationPreferenceForAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName, NotificationPreference.Off);

        // Assert
        Assert.NotEmpty(receivedEvents);

        // Validate event content
        var latestEvent = receivedEvents[^1];
        Assert.NotNull(latestEvent?.Events);
        Assert.Contains(latestEvent.Events, e => e.Name == "temperature-event");

        var eventData = latestEvent.Events.Find(e => e.Name == "temperature-event");
    }

    [Fact(Skip = "service doesn't support Update APIs yet")]
    public async Task MultipleEventStreamsAndErrorHandling()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        // Start observing asset updates
        var observeResponse = await client.SetNotificationPreferenceForAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName, NotificationPreference.On);
        Assert.Equal("Accepted", observeResponse.ResponsePayload);

        // Act - Update asset with multiple event streams including an error case
        var updateRequest = CreateUpdateAssetStatusRequest(DateTime.UtcNow);
        updateRequest.AssetStatus.Events =
        [
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
                    Details =
                    [
                        new DetailsSchemaElement
                        {
                            Code = "validation-error",
                            Message = "Schema validation failed",
                            CorrelationId = Guid.NewGuid().ToString()
                        }
                    ]
                }
            }
        ];

        var updatedAsset = await client.UpdateAssetStatusAsync(
            TestDevice_1_Name, TestEndpointName, updateRequest);

        // Get asset to verify state after update
        var asset = await client.GetAssetAsync(TestDevice_1_Name, TestEndpointName, TestAssetName);

        // Cleanup
        await client.SetNotificationPreferenceForAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName, NotificationPreference.Off);

        // Assert
        Assert.NotNull(updatedAsset);
        Assert.NotNull(asset);
        Assert.NotNull(asset.Events);
        Assert.Equal(2, asset.Events.Count);

        // Verify valid event stream
        var validEvent = asset.Events.Find(e => e.Name == "valid-event");
        Assert.NotNull(validEvent);

        // Verify error event stream
        var errorEvent = asset.Events.Find(e => e.Name == "error-event");
        Assert.NotNull(errorEvent);
    }

    [Fact(Skip = "service doesn't support Update APIs yet")]
    public async Task ObservationsPersistAfterReconnection()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        // Set up tracking for received events
        var receivedEvents = new List<Asset>();
        var firstEventReceived = new TaskCompletionSource<bool>();
        var reconnectionEventReceived = new TaskCompletionSource<bool>();
        var eventCounter = 0;

        // Set up event handler to track events
        client.OnReceiveAssetUpdateEventTelemetry += (assetName, asset) =>
        {
            _output.WriteLine($"Received asset event: {assetName}, count: {++eventCounter}");
            receivedEvents.Add(asset);

            // Signal based on which event we're processing
            if (eventCounter == 1)
            {
                firstEventReceived.TrySetResult(true);
            }
            else if (eventCounter > 1)
            {
                reconnectionEventReceived.TrySetResult(true);
            }
            return Task.CompletedTask;
        };

        // Start observing asset updates
        var observeResponse = await client.SetNotificationPreferenceForAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName, NotificationPreference.On);
        Assert.Equal("Accepted", observeResponse.ResponsePayload);

        // Act - Phase 1: Send an update and verify it's received
        var updateRequest1 = CreateUpdateAssetStatusRequest(DateTime.UtcNow);
        updateRequest1.AssetStatus.Events =
        [
            new AssetDatasetEventStreamStatus
            {
                Name = "pre-disconnect-event",
                MessageSchemaReference = new MessageSchemaReference
                {
                    SchemaName = "test-schema",
                    SchemaRegistryNamespace = "test-namespace",
                    SchemaVersion = "1.0"
                }
            }
        ];

        await client.UpdateAssetStatusAsync(
            TestDevice_1_Name, TestEndpointName, updateRequest1);

        // Wait for the first event
        try
        {
            await firstEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Did not receive first asset event update within timeout");
        }

        // Act - Phase 2: Simulate reconnection by disconnecting and recreating the mqtt client
        _output.WriteLine("Simulating disconnection by disconnecting client...");
        await mqttClient.DisconnectAsync();
        var savedClientId = mqttClient.ClientId;
        var mcs = ClientFactory.CreateMqttConnectionSettings();
        mcs.ClientId = savedClientId!;
        _output.WriteLine("Reconnecting client...");
        await mqttClient.ConnectAsync(mcs, CancellationToken.None);
        _output.WriteLine("Client reconnected.");

        // Act - Phase 3: Send another update after reconnection
        var updateRequest2 = CreateUpdateAssetStatusRequest(DateTime.UtcNow);
        updateRequest2.AssetStatus.Events =
        [
            new AssetDatasetEventStreamStatus
            {
                Name = "post-reconnect-event",
                MessageSchemaReference = new MessageSchemaReference
                {
                    SchemaName = "reconnect-schema",
                    SchemaRegistryNamespace = "test-namespace",
                    SchemaVersion = "2.0"
                }
            }
        ];

        await client.UpdateAssetStatusAsync(
            TestDevice_1_Name, TestEndpointName, updateRequest2);

        // Wait for the post-reconnection event
        try
        {
            await reconnectionEventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Did not receive asset event after reconnection within timeout");
        }

        // Cleanup
        await client.SetNotificationPreferenceForAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName, NotificationPreference.Off);

        // Assert
        var postReconnectEvents = receivedEvents.Where(e =>
            e.Events?.Any(evt => evt.Name == "post-reconnect-event") == true).ToList();

        Assert.NotEmpty(postReconnectEvents);

        // Verify the event data after reconnection
        var latestEvent = postReconnectEvents[^1];
        Assert.NotNull(latestEvent.Events);
        var eventData = latestEvent.Events.Find(e => e.Name == "post-reconnect-event");
        Assert.NotNull(eventData);
        //Assert.NotNull(eventData?.MessageSchemaReference);
        //Assert.Equal("reconnect-schema", eventData.MessageSchemaReference.SchemaName);
        //Assert.Equal("test-namespace", eventData.MessageSchemaReference.SchemaRegistryNamespace);
        //Assert.Equal("2.0", eventData.MessageSchemaReference.SchemaVersion);
    }

    [Fact(Skip = "service doesn't support Update APIs yet")]
    public async Task ReceiveTelemetryForProperDeviceUpdateWhenMultipleDevicesUpdated()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        var receivedEvents = new Dictionary<string, Device>();
        var eventReceived = new TaskCompletionSource<bool>();

        // Set up event handler to capture and validate events
        client.OnReceiveDeviceUpdateEventTelemetry += (deviceName, device) =>
        {
            _output.WriteLine($"Received device event: {deviceName}");
            receivedEvents.Add(deviceName, device);
            eventReceived.TrySetResult(true);
            return Task.CompletedTask;
        };

        // Start observing device updates
        await client.SetNotificationPreferenceForDeviceUpdatesAsync(TestDevice_1_Name, TestEndpointName, NotificationPreference.On);

        // Act - Update multiple devices to trigger notifications
        var updateRequest1 = CreateDeviceStatus(DateTime.UtcNow);
        await client.UpdateDeviceStatusAsync(TestDevice_1_Name, TestEndpointName, updateRequest1);

        var updateRequest2 = CreateDeviceStatus(DateTime.UtcNow.AddMinutes(1));
        await client.UpdateDeviceStatusAsync(TestDevice_2_Name, TestEndpointName, updateRequest2);

        // Wait for the event to be received or timeout
        try
        {
            await eventReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Did not receive device event update within timeout");
        }

        // Cleanup
        await client.SetNotificationPreferenceForDeviceUpdatesAsync(TestDevice_1_Name, TestEndpointName, NotificationPreference.Off);

        // Assert
        Assert.NotEmpty(receivedEvents);
        Assert.True(receivedEvents.ContainsKey(TestDevice_1_Name), $"Expected device event for {TestDevice_1_Name} not received");
        Assert.False(receivedEvents.ContainsKey(TestDevice_2_Name), $"Unexpected device event for test-thermostat received");
    }

    private CreateOrUpdateDiscoveredAssetRequest CreateCreateDetectedAssetRequest()
    {
        return new CreateOrUpdateDiscoveredAssetRequest
        {
            DiscoveredAssetName = TestAssetName,
            DiscoveredAsset = new DiscoveredAsset
            {
                DeviceRef = new AssetDeviceRef
                {
                    DeviceName = TestDevice_1_Name,
                    EndpointName = TestEndpointName
                }
            },
        };
    }

    private static DeviceStatus CreateDeviceStatus(DateTime timeStamp)
    {
        return new DeviceStatus
        {
            Config = new DeviceStatusConfig
            {
                Error = null,
                LastTransitionTime = timeStamp,
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
                    LastTransitionTime = timeStamp,
                    Version = 1
                }
            }
        };
    }

    private CreateOrUpdateDiscoveredDeviceRequestSchema CreateCreateDiscoveredDeviceRequest()
    {
        return new CreateOrUpdateDiscoveredDeviceRequestSchema
        {
            DiscoveredDeviceName = "test-discovered-device",
            DiscoveredDevice = new()
            {
                Manufacturer = "Test Manufacturer",
                Model = "Test Model",
                OperatingSystem = "Linux",
                OperatingSystemVersion = "1.0",
                ExternalDeviceId = "external-device-id-123",
                Endpoints = new()
                {
                    Inbound = new Dictionary<string, DiscoveredDeviceInboundEndpoint>
                    {
                        {
                            TestEndpointName,
                            new DiscoveredDeviceInboundEndpoint
                            {
                                Address = "http://example.com",
                                EndpointType = "my-rest-endpoint",
                                Version = "1.0",
                                SupportedAuthenticationMethods = new List<string> { "Basic", "OAuth2" }
                            }
                        }
                    },
                    Outbound = new DiscoveredDeviceOutboundEndpoints
                    {
                        Assigned = new Dictionary<string, DeviceOutboundEndpoint>
                        {
                            { "outbound-endpoint-1", new DeviceOutboundEndpoint { Address = "http://outbound.example.com", EndpointType = "rest" } }
                        }
                    }
                },
                Attributes = new Dictionary<string, string>
                {
                    { "attribute1", "value1" },
                    { "attribute2", "value2" }
                }
            }
        };
    }
}
