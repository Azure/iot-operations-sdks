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
    public async Task TriggerAssetTelemetryEventWhenObservedAsync() // this test causes the connector pod crash
    {
        // Create 2 ADR clients, each with different MQTT connections + client Ids
        ApplicationContext applicationContext = new();
        await using MqttSessionClient mqttClient1 = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(Guid.NewGuid().ToString());
        await using MqttSessionClient mqttClient2 = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync(Guid.NewGuid().ToString());
        await using AdrServiceClient adrClient1 = new(applicationContext, mqttClient1);
        await using AdrServiceClient adrClient2 = new(applicationContext, mqttClient2);

        var eventReceivedByClient1 = new TaskCompletionSource();
        adrClient1.OnReceiveAssetUpdateEventTelemetry += (source, _) =>
        {
            eventReceivedByClient1.TrySetResult();
            return Task.CompletedTask;
        };

        var eventReceivedByClient2 = new TaskCompletionSource();
        adrClient2.OnReceiveAssetUpdateEventTelemetry += (source, _) =>
        {
            eventReceivedByClient2.TrySetResult();
            return Task.CompletedTask;
        };

        // Client 1 observes a random asset, unrelated to the one that will change. Client 2 observes the asset that will change
        await adrClient1.SetNotificationPreferenceForAssetUpdatesAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), NotificationPreference.On);
        await adrClient2.SetNotificationPreferenceForAssetUpdatesAsync(TestDevice_1_Name, TestEndpointName, TestAssetName, NotificationPreference.On);

        // Update the asset
        UpdateAssetStatusRequest updateRequest = CreateUpdateAssetStatusRequest(DateTime.Now);
        await adrClient2.UpdateAssetStatusAsync(TestDevice_1_Name, TestEndpointName, updateRequest);

        // Client 1, which hadn't subscribed to the updated asset, receives the asset update event anyways.
        try
        {
            await eventReceivedByClient1.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            Assert.Fail("Did not receive asset update event within timeout");
        }
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
