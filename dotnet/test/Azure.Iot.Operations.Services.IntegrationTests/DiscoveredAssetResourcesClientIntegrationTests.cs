// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.IntegrationTests;

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.Akri;
using Azure.Iot.Operations.Services.Akri.DiscoveredAssetResources;
using Azure.Iot.Operations.Services.IntegrationTest;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

[Trait("Category", "DiscoveredAsset")]
public class DiscoveredAssetResourcesClientIntegrationTests
{

    [Fact]
    public async Task CreateOpcDiscoveredAsset()
    {

        await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("opcda-" + Guid.NewGuid().ToString());
        await using DiscoveredAssetResourcesClient mrpcClient = new(new ApplicationContext(), _mqttClient);
        CreateDiscoveredAssetRequestPayload request = new CreateDiscoveredAssetRequestPayload
        {
            CreateDiscoveredAssetRequest = new()
            {
                AssetEndpointProfileRef = "opc-asset-endpoint-profile",
                AssetName = "aiodasset-opc1",
                Datasets = new()
                        {
                            new ()
                            {
                                Name = "dataset1",
                                DataSetConfiguration = "{ \"test\": \"test\" }",
                                DataPoints = new ()
                                {
                                    new ()
                                    {
                                        Name = "test",
                                        DataPointConfiguration = "test",
                                        DataSource = "test",
                                    },
                                },
                                Topic = new ()
                                {
                                    Path = "akri/test",
                                    Retain = DiscoveredTopicRetain.Keep,
                                },
                            },
                        },
                DefaultDatasetsConfiguration = "{\"publishingInterval\":10,\"samplingInterval\":15,\"queueSize\":20}",
                DefaultEventsConfiguration = "{\"publishingInterval\":10,\"samplingInterval\":15,\"queueSize\":20}",
                DefaultTopic = new()
                {
                    Path = "akri/test",
                    Retain = DiscoveredTopicRetain.Keep,
                },
                DocumentationUri = "test",
                Events = new()
                        {
                            new ()
                            {
                                Name = "event1",
                                EventConfiguration = "{\"publishingInterval\":10,\"samplingInterval\":15,\"queueSize\":20}",
                                EventNotifier = "nsu=http://microsoft.com/Opc/OpcPlc/;s=FastUInt1",
                                Topic = new ()
                                {
                                    Path = "akri/test",
                                    Retain = DiscoveredTopicRetain.Never,
                                },
                            },
                        },
            },
        };

        var createDiscoveredAssetResponse = await mrpcClient.CreateDiscoveredAssetAsync(request);
        Assert.NotNull(createDiscoveredAssetResponse);
        Assert.Equal(createDiscoveredAssetResponse.Status, StatusSchema.Created);
    }

    [Fact]
    public async Task DuplicateOnvifDiscoveredAsset()
    {

        await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("onvifda-" + Guid.NewGuid().ToString());
        await using DiscoveredAssetResourcesClient mrpcClient = new(new ApplicationContext(), _mqttClient);

        CreateDiscoveredAssetRequestPayload dReq = new CreateDiscoveredAssetRequestPayload
        {
            CreateDiscoveredAssetRequest = new CreateDiscoveredAssetRequestSchema
            {
                AssetEndpointProfileRef = "floor-camera-WA01-002",
                AssetName = "aiodasset-onvif1",
                Manufacturer = "Contoso",
                Model = "C8455 Bispectral PTZ Camera",
                SerialNumber = "C8455-00030-2467",
                SoftwareRevision = "v1.2",
            },
        };

        var createDiscoveredAssetResponse = await mrpcClient.CreateDiscoveredAssetAsync(dReq);
        Assert.NotNull(createDiscoveredAssetResponse);
        Assert.Equal(createDiscoveredAssetResponse.Status, StatusSchema.Created);

        var createDiscoveredAssetResponseDup = await mrpcClient.CreateDiscoveredAssetAsync(dReq);
        Assert.NotNull(createDiscoveredAssetResponseDup);
        Assert.Equal(createDiscoveredAssetResponseDup.Status, StatusSchema.Duplicate);

    }


    [Fact]
    public async Task CreateOpcDiscoveredAssetEndpointProfile()
    {
        await using MqttSessionClient _mqttClient = await ClientFactory.CreateAndConnectClientAsyncFromEnvAsync("opcdaep-" + Guid.NewGuid().ToString());
        await using DiscoveredAssetResourcesClient mrpcClient = new(new ApplicationContext(), _mqttClient);

        CreateDiscoveredAssetEndpointProfileRequestPayload dReq = new CreateDiscoveredAssetEndpointProfileRequestPayload
        {
            CreateDiscoveredAssetEndpointProfileRequest = new CreateDiscoveredAssetEndpointProfileRequestSchema
            {
                AdditionalConfiguration = "{ \"test\": \"test\" }",
                DaepName = "aiodaep-opcua1",
                EndpointProfileType = "OpcUa",
                SupportedAuthenticationMethods = new List<SupportedAuthenticationMethodsSchemaElementSchema>
                        {
                            SupportedAuthenticationMethodsSchemaElementSchema.Anonymous,
                            SupportedAuthenticationMethodsSchemaElementSchema.Certificate,
                        },
                TargetAddress = "opc.tcp://192.168.10.2:43/onvif/device_service",
            },
        };

        var createDiscoveredAssetEndpointProfileResponse = await mrpcClient.CreateDiscoveredAssetEndpointProfileAsync(dReq);
        Assert.NotNull(createDiscoveredAssetEndpointProfileResponse);
        Assert.Equal(createDiscoveredAssetEndpointProfileResponse.Status, StatusSchema.Created);
    }
}
