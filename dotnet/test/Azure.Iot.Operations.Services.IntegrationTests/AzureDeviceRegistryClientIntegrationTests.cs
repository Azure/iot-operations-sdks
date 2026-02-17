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
using Azure.Iot.Operations.Protocol.Retry;

[Trait("Category", "ADR")]
public class AzureDeviceRegistryClientIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private const string ConnectorClientId = "test-connector-client";
    private const string TestDevice_1_Name = "my-thermostat";
    private const string TestDevice_2_Name = "test-thermostat";
    private const string TestEndpointName = "my-rest-endpoint";
    private const string TestAssetName = "my-rest-thermostat-asset";

    public AzureDeviceRegistryClientIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CanGetDeviceAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(ConnectorClientId);
        ApplicationContext applicationContext = new();
        await using AzureDeviceRegistryClient client = new(applicationContext, mqttClient, new NoRetryPolicy());

        // Act
        var device = await client.GetDeviceAsync(TestDevice_1_Name, TestEndpointName);

        // Assert
        _output.WriteLine($"Device: {TestDevice_1_Name}");
        Assert.NotNull(device);
        Assert.NotNull(device.Endpoints);
        Assert.NotNull(device.Endpoints.Inbound);
        Assert.Single(device.Endpoints.Inbound.Keys);
    }

    [Fact]
    public async Task GetDeviceNotFoundAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(ConnectorClientId);
        ApplicationContext applicationContext = new();
        await using AzureDeviceRegistryClient client = new(applicationContext, mqttClient, new NoRetryPolicy());

        // Act
        await Assert.ThrowsAsync<AkriServiceErrorException>(async () => await client.GetDeviceAsync("this_device_does_not_exist", TestEndpointName));
    }

    [Fact]
    public async Task GetDeviceThrowsAkriServiceErrorExceptionWhenDeviceNotFoundAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(ConnectorClientId);
        ApplicationContext applicationContext = new();
        await using AzureDeviceRegistryClient client = new(applicationContext, mqttClient, new NoRetryPolicy());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AkriServiceErrorException>(
            () => client.GetDeviceAsync("non-existent-device", TestEndpointName));

        _output.WriteLine($"Expected exception: {exception.Message}");
        Assert.NotNull(exception.AkriServiceError);
        Assert.Equal(Code.KubeError, exception.AkriServiceError.Code);
    }

    [Fact]
    public async Task CanUpdateDeviceStatusAsync()
    {
        // Arrange
        var expectedTime = DateTime.Parse("2023-10-01T00:00:00Z");
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(ConnectorClientId);
        ApplicationContext applicationContext = new();
        await using AzureDeviceRegistryClient client = new(applicationContext, mqttClient, new NoRetryPolicy());

        var status = new DeviceStatus
        {
            Config = new ConfigStatus
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

    [Fact]
    public async Task TriggerDeviceTelemetryEventWhenObservedAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(ConnectorClientId);
        ApplicationContext applicationContext = new();
        await using AzureDeviceRegistryClient client = new(applicationContext, mqttClient, new NoRetryPolicy());

        var eventReceived = new TaskCompletionSource<bool>();
        client.OnReceiveDeviceUpdateEventTelemetry += (deviceName, inboundEndpointName, _) =>
        {
            _output.WriteLine($"Device update received from: {deviceName}_{inboundEndpointName}");
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

    [Fact]
    public async Task DoNotTriggerTelemetryEventAfterUnobserveDeviceAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(ConnectorClientId);
        ApplicationContext applicationContext = new();
        await using AzureDeviceRegistryClient client = new(applicationContext, mqttClient, new NoRetryPolicy());

        var firstEventReceived = new TaskCompletionSource<bool>();
        var secondEventReceived = new TaskCompletionSource<bool>();
        var eventCounter = 0;

        client.OnReceiveDeviceUpdateEventTelemetry += (deviceName, inboundEndpointName, _) =>
        {
            _output.WriteLine($"Device update received from: {deviceName}_{inboundEndpointName}");
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
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(ConnectorClientId);
        ApplicationContext applicationContext = new();
        await using AzureDeviceRegistryClient client = new(applicationContext, mqttClient, new NoRetryPolicy());

        // Act
        var asset = await client.GetAssetAsync(TestDevice_1_Name, TestEndpointName, TestAssetName);

        // Assert
        _output.WriteLine($"Asset: {TestAssetName}");
        Assert.NotNull(asset);
        Assert.NotNull(asset.Datasets);
        Assert.Single(asset.Datasets);
    }

    [Fact]
    public async Task CanUpdateAssetStatusAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(ConnectorClientId);
        ApplicationContext applicationContext = new();
        await using AzureDeviceRegistryClient client = new(applicationContext, mqttClient, new NoRetryPolicy());

        var expectedTime = DateTime.UtcNow;

        UpdateAssetStatusRequest request = CreateUpdateAssetStatusRequest(expectedTime);

        // Act
        var updatedAsset = await client.UpdateAssetStatusAsync(TestDevice_1_Name, TestEndpointName, request);

        // Assert
        Assert.NotNull(updatedAsset);
        Assert.NotNull(updatedAsset.Config);
        Assert.Equal(expectedTime, updatedAsset.Config.LastTransitionTime);
    }

    [Fact]
    public async Task TriggerAssetTelemetryEventWhenObservedAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(ConnectorClientId);
        ApplicationContext applicationContext = new();
        await using AzureDeviceRegistryClient client = new(applicationContext, mqttClient, new NoRetryPolicy());

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

    [Fact]
    public async Task DoNotTriggerTelemetryEventAfterUnobserveAssetAsync()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(ConnectorClientId);
        ApplicationContext applicationContext = new();
        await using AzureDeviceRegistryClient client = new(applicationContext, mqttClient, new NoRetryPolicy());

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
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(ConnectorClientId);
        ApplicationContext applicationContext = new();
        await using AzureDeviceRegistryClient client = new(applicationContext, mqttClient, new NoRetryPolicy());

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
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(ConnectorClientId);
        ApplicationContext applicationContext = new();
        await using AzureDeviceRegistryClient client = new(applicationContext, mqttClient, new NoRetryPolicy());

        var request = CreateCreateDiscoveredDeviceRequest();

        // Act
        var response = await client.CreateOrUpdateDiscoveredDeviceAsync(request, TestEndpointName);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.DiscoveredDeviceResponse.DiscoveryId);
        Assert.NotEmpty(response.DiscoveredDeviceResponse.DiscoveryId);
        Assert.Equal("test-discovered-device", response.DiscoveredDeviceResponse.DiscoveryId);
        _output.WriteLine($"Discovered device created with name: {response.DiscoveredDeviceResponse.DiscoveryId}");
    }

    [Fact]
    public async Task ReceiveTelemetryForProperDeviceUpdateWhenMultipleDevicesUpdated()
    {
        // Arrange
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(ConnectorClientId);
        ApplicationContext applicationContext = new();
        await using AzureDeviceRegistryClient client = new(applicationContext, mqttClient, new NoRetryPolicy());

        var receivedEvents = new Dictionary<string, Device>();
        var eventReceived = new TaskCompletionSource<bool>();

        // Set up event handler to capture and validate events
        client.OnReceiveDeviceUpdateEventTelemetry += (deviceName, inboundEndpointName, device) =>
        {
            _output.WriteLine($"Received device event: {deviceName}_{inboundEndpointName}");
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


    [Fact]
    public async Task CanClearErrorFromDeviceStatusAsync()
    {
        // Arrange
        var expectedTime = DateTime.Parse("2023-10-01T00:00:00Z");
        await using MqttSessionClient mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(ConnectorClientId);
        ApplicationContext applicationContext = new();
        await using AzureDeviceRegistryClient client = new(applicationContext, mqttClient, new NoRetryPolicy());

        var status = new DeviceStatus
        {
            Config = new ConfigStatus
            {
                Error = new ConfigError()
                {
                    Code = "someError",
                    Message = "bad stuff",
                    Details = new List<ConfigErrorDetails>()
                    {
                        {
                            new ConfigErrorDetails()
                            {
                                Code = "someError"
                            }
                        }
                    }
                },
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
        Assert.NotNull(updatedDevice.Config.Error);
        Assert.Equal(expectedTime, updatedDevice.Config.LastTransitionTime);

        // clear the error, update the device again, check that the error was cleared
        status.Config.Error = null;
        updatedDevice = await client.UpdateDeviceStatusAsync(TestDevice_1_Name, TestEndpointName, status);
        Assert.NotNull(updatedDevice.Config);
        Assert.Null(updatedDevice.Config.Error);
    }
    private CreateOrUpdateDiscoveredAssetRequest CreateCreateDetectedAssetRequest()
    {
        var asset = new CreateOrUpdateDiscoveredAssetRequest
        {
            DiscoveredAssetName = TestAssetName,
            DiscoveredAsset = new DiscoveredAsset
            {
                DeviceRef = new AssetDeviceRef
                {
                    DeviceName = TestDevice_1_Name,
                    EndpointName = TestEndpointName
                },
            },
        };

        return asset;
    }

    private static DeviceStatus CreateDeviceStatus(DateTime timeStamp)
    {
        return new DeviceStatus
        {
            Config = new ConfigStatus
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
                Config = new ConfigStatus
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
                                SupportedAuthenticationMethods = new List<string> { "Anonymous" }
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
