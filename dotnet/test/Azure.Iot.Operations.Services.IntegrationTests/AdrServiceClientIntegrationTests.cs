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
        var device = await client.GetDeviceAsync("my-thermostat", "my-rest-endpoint");

        // Assert
        _output.WriteLine($"Device: {device.Name}");
        Assert.NotNull(device);
        Assert.Equal("my-thermostat", device.Name);
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
        Assert.Equal("KubeError", exception.AkriServiceError.Code);
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
                LastTransitionTime = DateTime.Parse("2023-10-01T00:00:00Z"),
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
        Device updatedDevice = await client.UpdateDeviceStatusAsync(TestDevice_1_Name, TestEndpointName, status);

        // Assert
        Assert.NotNull(updatedDevice);
        Assert.Equal(TestDevice_1_Name, updatedDevice.Name);
        _output.WriteLine($"Updated device: {updatedDevice.Name}");
    }

    [Fact]
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
        await client.ObserveDeviceEndpointUpdatesAsync(TestDevice_1_Name, TestEndpointName);

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

    [Fact]
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
        await client.ObserveDeviceEndpointUpdatesAsync(TestDevice_1_Name, TestEndpointName);

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
        await client.UnobserveDeviceEndpointUpdatesAsync(TestDevice_1_Name, TestEndpointName);

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
        var request = new GetAssetRequest
        {
            AssetName = TestAssetName
        };

        // Act
        var asset = await client.GetAssetAsync(TestDevice_1_Name, TestEndpointName, request);

        // Assert
        _output.WriteLine($"Asset: {asset.Name}");
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
        var updatedAsset = await client.UpdateAssetStatusAsync(TestDevice_1_Name, TestEndpointName, request);

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

        var eventReceived = new TaskCompletionSource<bool>();
        client.OnReceiveAssetUpdateEventTelemetry += (source, _) =>
        {
            _output.WriteLine($"Asset update received from: {source}");
            eventReceived.TrySetResult(true);
            return Task.CompletedTask;
        };

        // Act - Observe
        await client.ObserveAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName);

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

    [Fact]
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
        await client.ObserveAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName);

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
        await client.UnobserveAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName);

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
        Assert.NotEmpty(result.DiscoveryId);
        _output.WriteLine($"Detected asset created with DiscoveryId: {result.DiscoveryId}");
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
        Assert.NotNull(response.DiscoveryId);
        Assert.NotEmpty(response.DiscoveryId);
        Assert.Equal("test-discovered-device", response.DiscoveryId);
        _output.WriteLine($"Discovered device created with name: {response.DiscoveryId}");
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
            _output.WriteLine($"Received asset event: {asset.Name}");
            receivedEvents.Add(asset);

            // Verify events data is present and correctly structured
            if (asset.Status?.Events is { Count: > 0 })
            {
                _output.WriteLine($"Events count: {asset.Status.Events.Count}");
                foreach (var evt in asset.Status.Events)
                {
                    _output.WriteLine($"Event: {evt.Name}, Schema: {evt.MessageSchemaReference?.SchemaName}");
                }
                eventReceived.TrySetResult(true);
            }
            return Task.CompletedTask;
        };

        // Start observing asset updates
        await client.ObserveAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName);

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
        await client.UnobserveAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName);

        // Assert
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
            TestDevice_1_Name, TestEndpointName, TestAssetName);
        Assert.Equal(NotificationResponse.Accepted, observeResponse);

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
        var asset = await client.GetAssetAsync(
            TestDevice_1_Name,
            TestEndpointName,
            new GetAssetRequest { AssetName = TestAssetName });

        // Cleanup
        await client.UnobserveAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName);

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

    [Fact]
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
        client.OnReceiveAssetUpdateEventTelemetry += (_, asset) =>
        {
            _output.WriteLine($"Received asset event: {asset.Name}, count: {++eventCounter}");
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
        var observeResponse = await client.ObserveAssetUpdatesAsync(
            TestDevice_1_Name, TestEndpointName, TestAssetName);
        Assert.Equal(NotificationResponse.Accepted, observeResponse);

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
        await client.UnobserveAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName);

        // Assert
        var postReconnectEvents = receivedEvents.Where(e =>
            e.Status?.Events?.Any(evt => evt.Name == "post-reconnect-event") == true).ToList();

        Assert.NotEmpty(postReconnectEvents);

        // Verify the event data after reconnection
        var latestEvent = postReconnectEvents[^1];
        Assert.NotNull(latestEvent.Status?.Events);
        var eventData = latestEvent.Status.Events.Find(e => e.Name == "post-reconnect-event");
        Assert.NotNull(eventData?.MessageSchemaReference);
        Assert.Equal("reconnect-schema", eventData.MessageSchemaReference.SchemaName);
        Assert.Equal("test-namespace", eventData.MessageSchemaReference.SchemaRegistryNamespace);
        Assert.Equal("2.0", eventData.MessageSchemaReference.SchemaVersion);
    }

    [Fact]
    public async Task ReceiveTelemetryForProperDeviceUpdateWhenMultipleDevicesUpdated()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync();
        ApplicationContext applicationContext = new();
        await using AdrServiceClient client = new(applicationContext, mqttClient, ConnectorClientId);

        var receivedEvents = new List<Device>();
        var eventReceived = new TaskCompletionSource<bool>();

        // Set up event handler to capture and validate events
        client.OnReceiveDeviceUpdateEventTelemetry += (_, device) =>
        {
            _output.WriteLine($"Received device event: {device.Name}");
            receivedEvents.Add(device);
            eventReceived.TrySetResult(true);
            return Task.CompletedTask;
        };

        // Start observing device updates
        await client.ObserveDeviceEndpointUpdatesAsync(TestDevice_1_Name, TestEndpointName);

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
        await client.UnobserveDeviceEndpointUpdatesAsync(TestDevice_1_Name, TestEndpointName);

        // Assert
        Assert.NotEmpty(receivedEvents);
        Assert.True(receivedEvents.Any(d => d.Name == TestDevice_1_Name), $"Expected device event for {TestDevice_1_Name} not received");
        Assert.True(receivedEvents.All(d => d.Name != TestDevice_2_Name), $"Unexpected device event for test-thermostat received");
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

    private CreateDiscoveredAssetEndpointProfileRequest CreateCreateDiscoveredDeviceRequest()
    {
        return new CreateDiscoveredAssetEndpointProfileRequest
        {
            Name = "test-discovered-device",
            Manufacturer = "Test Manufacturer",
            Model = "Test Model",
            OperatingSystem = "Linux",
            OperatingSystemVersion = "1.0",
            ExternalDeviceId = "external-device-id-123",
            Endpoints = new DiscoveredDeviceEndpoint
            {
                Inbound = new Dictionary<string, DiscoveredDeviceInboundEndpoint>
                {
                    {
                        TestEndpointName,
                        new DiscoveredDeviceInboundEndpoint
                        {
                            Address = "http://example.com",
                            EndpointType = "rest",
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
        };
    }
}
