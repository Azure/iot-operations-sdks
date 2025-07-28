// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Xunit;
using AdrBaseService = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using AepTypeService = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.DeviceDiscoveryService;

namespace Azure.Iot.Operations.Services.UnitTests.AssetAndDeviceRegistry;

/*
public class ProtocolConverterTests
{
    [Fact]
    public void GetAssetRequest_ToProtocol_ShouldConvertAllProperties()
    {
        // Arrange - Create a complete GetAssetRequest with all properties set
        var source = new GetAssetRequest
        {
            AssetName = "test-asset-name",
        };

        // Act - Convert to protocol format
        var result = source.ToProtocol();

        // Assert - Verify all properties are converted correctly
        Assert.NotNull(result);
        Assert.Equal("test-asset-name", result.AssetName);
    }

    [Fact]
    public void UpdateAssetStatusRequest_ToProtocol_ShouldConvertAllProperties()
    {
        // Arrange - Create a complete UpdateAssetStatusRequest with all nested properties
        var source = new UpdateAssetStatusRequest
        {
            AssetName = "test-asset",
            AssetStatus = new AssetStatus
            {
                Config = new AssetConfigStatus
                {
                    LastTransitionTime = "2023-01-01T00:00:00Z",
                    Version = 1,
                    Error = new ConfigError
                    {
                        Code = "config-error-code",
                        Message = "config-error-message",
                        InnerError = new Dictionary<string, string> { { "inner-key", "inner-value" } },
                        Details =
                        [
                            new()
                            {
                                Code = "detail-code",
                                Message = "detail-message",
                                Info = "detail-info",
                                CorrelationId = "correlation-id"
                            }
                        ]
                    },
                },
                Datasets =
                [
                    new()
                    {
                        Name = "dataset1",
                        Error = new ConfigError
                        {
                            Code = "dataset-error-code",
                            Message = "dataset-error-message"
                        },
                        MessageSchemaReference = new MessageSchemaReference
                        {
                            SchemaName = "schema1",
                            SchemaRegistryNamespace = "namespace1",
                            SchemaVersion = "1.0"
                        }
                    }
                ],
                Events =
                [
                    new()
                    {
                        Name = "event1",
                        MessageSchemaReference = new MessageSchemaReference
                        {
                            SchemaName = "event-schema",
                            SchemaRegistryNamespace = "event-namespace",
                            SchemaVersion = "1.0"
                        }
                    }
                ],
                ManagementGroups =
                [
                    new()
                    {
                        Name = "mgmt-group1",
                        Actions =
                        [
                            new()
                            {
                                Name = "action1",
                                Error = new ConfigError
                                {
                                    Code = "action-error",
                                    Message = "action-error-message"
                                },
                                RequestMessageSchemaReference = new MessageSchemaReference
                                {
                                    SchemaName = "req-schema",
                                    SchemaRegistryNamespace = "req-namespace",
                                    SchemaVersion = "1.0"
                                },
                                ResponseMessageSchemaReference = new MessageSchemaReference
                                {
                                    SchemaName = "resp-schema",
                                    SchemaRegistryNamespace = "resp-namespace",
                                    SchemaVersion = "1.0"
                                }
                            }
                        ]
                    }
                ],
                Streams =
                [
                    new()
                    {
                        Name = "stream1",
                        Error = new ConfigError
                        {
                            Code = "stream-error-code",
                            Message = "stream-error-message"
                        },
                        MessageSchemaReference = new MessageSchemaReference
                        {
                            SchemaName = "stream-schema",
                            SchemaRegistryNamespace = "stream-namespace",
                            SchemaVersion = "1.0"
                        }
                    }
                ]
            }
        };

        // Act - Convert to protocol format
        var result = source.ToProtocol();

        // Assert - Verify all properties are converted correctly
        Assert.NotNull(result);
        Assert.Equal("test-asset", result.AssetStatusUpdate.AssetName);
        Assert.NotNull(result.AssetStatusUpdate.AssetStatus);

        // Verify Config
        Assert.NotNull(result.AssetStatusUpdate.AssetStatus.Config);
        Assert.Equal("config-error-code", result.AssetStatusUpdate.AssetStatus.Config.Error!.Code);
        Assert.Equal("config-error-message", result.AssetStatusUpdate.AssetStatus.Config.Error.Message);
        Assert.Equal("inner-value", result.AssetStatusUpdate.AssetStatus.Config.Error.InnerError!["inner-key"]);
        Assert.Single(result.AssetStatusUpdate.AssetStatus.Config.Error.Details!);
        Assert.Equal("detail-code", result.AssetStatusUpdate.AssetStatus.Config.Error.Details![0].Code);

        // Verify Datasets
        Debug.Assert(result.AssetStatusUpdate.AssetStatus.Datasets != null, "result.AssetStatusUpdate.AssetStatus.Datasets != null");
        Assert.Single(result.AssetStatusUpdate.AssetStatus.Datasets);
        Assert.Equal("dataset1", result.AssetStatusUpdate.AssetStatus.Datasets[0].Name);
        Assert.Equal("dataset-error-message", result.AssetStatusUpdate.AssetStatus.Datasets[0].Error!.Message);
        Assert.Equal("schema1", result.AssetStatusUpdate.AssetStatus.Datasets[0].MessageSchemaReference!.SchemaName);

        // Verify Events
        Debug.Assert(result.AssetStatusUpdate.AssetStatus.Events != null, "result.AssetStatusUpdate.AssetStatus.Events != null");
        Assert.Single(result.AssetStatusUpdate.AssetStatus.Events);
        Assert.Equal("event1", result.AssetStatusUpdate.AssetStatus.Events[0].Name);
        Assert.Equal("event-schema", result.AssetStatusUpdate.AssetStatus.Events[0].MessageSchemaReference!.SchemaName);

        // Verify ManagementGroups
        Debug.Assert(result.AssetStatusUpdate.AssetStatus.ManagementGroups != null, "result.AssetStatusUpdate.AssetStatus.ManagementGroups != null");
        Assert.Single(result.AssetStatusUpdate.AssetStatus.ManagementGroups);
        Assert.Equal("mgmt-group1", result.AssetStatusUpdate.AssetStatus.ManagementGroups[0].Name);
        Assert.Single(result.AssetStatusUpdate.AssetStatus.ManagementGroups[0].Actions!);
        Assert.Equal("action1", result.AssetStatusUpdate.AssetStatus.ManagementGroups[0].Actions![0].Name);
        Assert.Equal("action-error", result.AssetStatusUpdate.AssetStatus.ManagementGroups[0].Actions![0].Error!.Code);

        // Verify Streams
        Debug.Assert(result.AssetStatusUpdate.AssetStatus.Streams != null, "result.AssetStatusUpdate.AssetStatus.Streams != null");
        Assert.Single(result.AssetStatusUpdate.AssetStatus.Streams);
        Assert.Equal("stream1", result.AssetStatusUpdate.AssetStatus.Streams[0].Name);
        Assert.Equal("stream-schema", result.AssetStatusUpdate.AssetStatus.Streams[0].MessageSchemaReference!.SchemaName);
        Assert.Equal("stream-namespace", result.AssetStatusUpdate.AssetStatus.Streams[0].MessageSchemaReference!.SchemaRegistryNamespace);
        Assert.Equal("stream-error-code", result.AssetStatusUpdate.AssetStatus.Streams[0].Error!.Code);
    }

    [Fact]
    public void CreateDetectedAssetRequest_ToProtocol_ShouldConvertAllProperties()
    {
        // Arrange - Create a complete CreateDetectedAssetRequest with all properties set
        var source = new DiscoveredAsset
        {
            AssetName = "test-detected-asset",
            AssetEndpointProfileRef = "endpoint-profile-ref",
            Datasets =
            [
                new()
                {
                    Name = "dataset1",
                    DataSetConfiguration = "dataset-config",
                    DataPoints =
                    [
                        new()
                        {
                            Name = "datapoint1",
                            DataPointConfiguration = "datapoint-config",
                            DataSource = "data-source",
                            LastUpdatedOn = "2023-01-01T00:00:00Z"
                        }
                    ],
                    Topic = new Topic
                    {
                        Path = "dataset/topic",
                        Retain = Retain.Never
                    }
                }
            ],
            DefaultDatasetsConfiguration = "default-dataset-config",
            DefaultEventsConfiguration = "default-events-config",
            DefaultTopic = new Topic
            {
                Path = "default/topic",
                Retain = Retain.Keep
            },
            DocumentationUri = "https://docs.example.com",
            Events =
            [
                new ()
                {
                    EventNotifier = "event-notifier",
                    Name = "event1",
                    EventConfiguration = "event-config",
                    Topic = new Topic
                    {
                        Path = "event/topic",
                        Retain = Retain.Keep
                    },
                    LastUpdatedOn = "2023-01-01T00:00:00Z"
                }
            ],
            HardwareRevision = "hw-rev-1.0",
            Manufacturer = "Test Manufacturer",
            ManufacturerUri = "https://manufacturer.example.com",
            Model = "Test Model",
            ProductCode = "PROD-123",
            SerialNumber = "SN12345",
            SoftwareRevision = "sw-rev-2.0"
        };

        // Act - Convert to protocol format
        var result = source.ToProtocol();

        // Assert - Verify all properties are converted correctly
        Assert.NotNull(result);
        Assert.NotNull(result.DetectedAsset);

        // Verify base properties
        Assert.Equal("test-detected-asset", result.DetectedAsset.AssetName);
        Assert.Equal("endpoint-profile-ref", result.DetectedAsset.AssetEndpointProfileRef);
        Assert.Equal("default-dataset-config", result.DetectedAsset.DefaultDatasetsConfiguration);
        Assert.Equal("default-events-config", result.DetectedAsset.DefaultEventsConfiguration);
        Assert.Equal("https://docs.example.com", result.DetectedAsset.DocumentationUri);
        Assert.Equal("hw-rev-1.0", result.DetectedAsset.HardwareRevision);
        Assert.Equal("Test Manufacturer", result.DetectedAsset.Manufacturer);
        Assert.Equal("https://manufacturer.example.com", result.DetectedAsset.ManufacturerUri);
        Assert.Equal("Test Model", result.DetectedAsset.Model);
        Assert.Equal("PROD-123", result.DetectedAsset.ProductCode);
        Assert.Equal("SN12345", result.DetectedAsset.SerialNumber);
        Assert.Equal("sw-rev-2.0", result.DetectedAsset.SoftwareRevision);

        // Verify DefaultTopic
        Assert.NotNull(result.DetectedAsset.DefaultTopic);
        Assert.Equal("default/topic", result.DetectedAsset.DefaultTopic.Path);
        Assert.Equal(AdrBaseService.Retain.Keep, result.DetectedAsset.DefaultTopic.Retain);

        // Verify Datasets
        Assert.NotNull(result.DetectedAsset.Datasets);
        Assert.Single(result.DetectedAsset.Datasets);
        Assert.Equal("dataset1", result.DetectedAsset.Datasets[0].Name);
        Assert.Equal("dataset-config", result.DetectedAsset.Datasets[0].DataSetConfiguration);

        // Verify DataPoints in Dataset
        Assert.NotNull(result.DetectedAsset.Datasets[0].DataPoints);
        Assert.Single(result.DetectedAsset.Datasets[0].DataPoints!);
        Assert.Equal("datapoint1", result.DetectedAsset.Datasets[0].DataPoints![0].Name);
        Assert.Equal("datapoint-config", result.DetectedAsset.Datasets[0].DataPoints![0].DataPointConfiguration);
        Assert.Equal("data-source", result.DetectedAsset.Datasets[0].DataPoints![0].DataSource);
        Assert.Equal("2023-01-01T00:00:00Z", result.DetectedAsset.Datasets[0].DataPoints![0].LastUpdatedOn);

        // Verify Dataset Topic
        Assert.NotNull(result.DetectedAsset.Datasets[0].Topic);
        Assert.Equal("dataset/topic", result.DetectedAsset.Datasets[0].Topic!.Path);
        Assert.Equal(AdrBaseService.Retain.Never, result.DetectedAsset.Datasets[0].Topic!.Retain);

        // Verify Events
        Assert.NotNull(result.DetectedAsset.Events);
        Assert.Single(result.DetectedAsset.Events);
        Assert.Equal("event-notifier", result.DetectedAsset.Events[0].EventNotifier);
        Assert.Equal("event1", result.DetectedAsset.Events[0].Name);
        Assert.Equal("event-config", result.DetectedAsset.Events[0].EventConfiguration);
        Assert.Equal("2023-01-01T00:00:00Z", result.DetectedAsset.Events[0].LastUpdatedOn);

        // Verify Event Topic
        Assert.NotNull(result.DetectedAsset.Events[0].Topic);
        Assert.Equal("event/topic", result.DetectedAsset.Events[0].Topic!.Path);
        Assert.Equal(AdrBaseService.Retain.Keep, result.DetectedAsset.Events[0].Topic!.Retain);
    }

    [Fact]
    public void CreateDiscoveredAssetEndpointProfileRequest_ToProtocol_ShouldConvertAllProperties()
    {
        // Arrange - Create a complete CreateDiscoveredAssetEndpointProfileRequest with all properties set
        var source = new CreateDiscoveredAssetEndpointProfileRequest
        {
            Name = "daep-test-name",
            EndpointProfileType = "OpcUa",
            TargetAddress = "opc.tcp://myserver:4840",
            AdditionalConfiguration = "key1=value1,key2=value2,securityMode=SignAndEncrypt",
            SupportedAuthenticationMethods =
            [
                SupportedAuthenticationMethodsSchemaElement.Anonymous,
                SupportedAuthenticationMethodsSchemaElement.UsernamePassword
            ]
        };

        // Act - Convert to protocol format
        var result = source.ToProtocol();

        // Assert - Verify all properties are converted correctly
        Assert.NotNull(result);
        Assert.NotNull(result.DiscoveredAssetEndpointProfile);

        // Verify base properties
        Assert.Equal("daep-test-name", result.DiscoveredAssetEndpointProfile.DaepName);
        Assert.Equal("OpcUa", result.DiscoveredAssetEndpointProfile.EndpointProfileType);
        Assert.Equal("opc.tcp://myserver:4840", result.DiscoveredAssetEndpointProfile.TargetAddress);

        // Verify additional configuration
        Assert.NotNull(result.DiscoveredAssetEndpointProfile.AdditionalConfiguration);
        Assert.Equal("key1=value1,key2=value2,securityMode=SignAndEncrypt", result.DiscoveredAssetEndpointProfile.AdditionalConfiguration);

        // Verify auth methods
        Assert.NotNull(result.DiscoveredAssetEndpointProfile.SupportedAuthenticationMethods);
        Assert.Equal(2, result.DiscoveredAssetEndpointProfile.SupportedAuthenticationMethods.Count);
        Assert.Contains(AepTypeService.SupportedAuthenticationMethodsSchemaElementSchema.Anonymous,
            result.DiscoveredAssetEndpointProfile.SupportedAuthenticationMethods);
        Assert.Contains(AepTypeService.SupportedAuthenticationMethodsSchemaElementSchema.UsernamePassword,
            result.DiscoveredAssetEndpointProfile.SupportedAuthenticationMethods);
    }

    [Fact]
    public void DeviceStatus_ToProtocol_ShouldConvertAllProperties()
    {
        // Arrange - Create a complete DeviceStatus with all nested properties
        var source = new DeviceStatus
        {
            Config = new DeviceStatusConfig
            {
                LastTransitionTime = "2023-01-01T00:00:00Z",
                Version = 5,
                Error = new ConfigError
                {
                    Code = "device-config-error-code",
                    Message = "device-config-error-message",
                    InnerError = new Dictionary<string, string> { { "inner-key", "inner-value" } },
                    Details =
                    [
                        new()
                        {
                            Code = "detail-code",
                            Message = "detail-message",
                            Info = "detail-info",
                            CorrelationId = "correlation-id"
                        }
                    ]
                }
            },
            Endpoints = new DeviceStatusEndpoint
            {
                Inbound = new Dictionary<string, DeviceStatusInboundEndpointSchemaMapValue>
                {
                    {
                        "endpoint1",
                        new DeviceStatusInboundEndpointSchemaMapValue
                        {
                            Error = new ConfigError
                            {
                                Code = "endpoint-error-code",
                                Message = "endpoint-error-message"
                            }
                        }
                    },
                    {
                        "endpoint2",
                        new DeviceStatusInboundEndpointSchemaMapValue
                        {
                            Error = new ConfigError
                            {
                                Code = "endpoint2-error-code",
                                Message = "endpoint2-error-message"
                            }
                        }
                    }
                }
            }
        };

        // Act - Convert to protocol format
        var result = source.ToProtocol();

        // Assert - Verify all properties are converted correctly
        Assert.NotNull(result);

        // Verify Config
        Assert.NotNull(result.Config);
        Assert.Equal("2023-01-01T00:00:00Z", result.Config.LastTransitionTime);
        Assert.Equal((ulong)5, result.Config.Version);
        Assert.NotNull(result.Config.Error);
        Assert.Equal("device-config-error-code", result.Config.Error.Code);
        Assert.Equal("device-config-error-message", result.Config.Error.Message);
        Assert.NotNull(result.Config.Error.InnerError);
        Assert.Equal("inner-value", result.Config.Error.InnerError["inner-key"]);

        // Verify Details in Config.Error
        Assert.NotNull(result.Config.Error.Details);
        Assert.Single(result.Config.Error.Details);
        Assert.Equal("detail-code", result.Config.Error.Details[0].Code);
        Assert.Equal("detail-message", result.Config.Error.Details[0].Message);
        Assert.Equal("detail-info", result.Config.Error.Details[0].Info);
        Assert.Equal("correlation-id", result.Config.Error.Details[0].CorrelationId);

        // Verify Endpoints
        Assert.NotNull(result.Endpoints);
        Assert.NotNull(result.Endpoints.Inbound);
        Assert.Equal(2, result.Endpoints.Inbound.Count);

        // Verify first endpoint
        Assert.Contains("endpoint1", result.Endpoints.Inbound.Keys);
        Assert.NotNull(result.Endpoints.Inbound["endpoint1"].Error);
        Assert.Equal("endpoint-error-code", result.Endpoints.Inbound["endpoint1"].Error!.Code);
        Assert.Equal("endpoint-error-message", result.Endpoints.Inbound["endpoint1"].Error!.Message);

        // Verify second endpoint
        Assert.Contains("endpoint2", result.Endpoints.Inbound.Keys);
        Assert.NotNull(result.Endpoints.Inbound["endpoint2"].Error);
        Assert.Equal("endpoint2-error-code", result.Endpoints.Inbound["endpoint2"].Error!.Code);
        Assert.Equal("endpoint2-error-message", result.Endpoints.Inbound["endpoint2"].Error!.Message);
    }
}
*/

