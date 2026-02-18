using System.Text.Json;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.DeviceDiscoveryService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Xunit;
using AdrBaseService = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;

namespace Azure.Iot.Operations.Services.UnitTests.AssetAndDeviceRegistry
{
    /*
    public class ModelsConverterTests
    {
        [Fact]
        public void AssetStatus_ToModel_ShouldConvertAllProperties()
        {
            // Arrange - Create complete source AssetStatus with all properties
            var source = new AdrBaseService.AssetStatus
            {
                Config = new AdrBaseService.AssetConfigStatusSchema
                {
                    Error = new AdrBaseService.ConfigError
                    {
                        Code = "error-code",
                        Message = "error-message",
                        InnerError = new Dictionary<string, string> { { "inner-error", "inner-error-message" } },
                        Details =
                        [
                            new()
                            {
                                Code = "detail-code",
                                Message = "detail-message",
                                Info = "info",
                                CorrelationId = "correlation-id"
                            }
                        ]
                    },
                    LastTransitionTime = "2023-01-01T00:00:00Z",
                    Version = 1
                },
                Datasets =
                [
                    new()
                    {
                        Name = "dataset1",
                        Error = new AdrBaseService.ConfigError
                        {
                            Code = "dataset-error",
                            Message = "dataset-error-message"
                        },
                        MessageSchemaReference = new AdrBaseService.MessageSchemaReference
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
                        MessageSchemaReference = new AdrBaseService.MessageSchemaReference
                        {
                            SchemaName = "event-schema",
                            SchemaRegistryNamespace = "event-namespace",
                            SchemaVersion = "2.0"
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
                                Error = new AdrBaseService.ConfigError
                                {
                                    Code = "action-error",
                                    Message = "action-error-message"
                                },
                                RequestMessageSchemaReference = new AdrBaseService.MessageSchemaReference
                                {
                                    SchemaName = "req-schema",
                                    SchemaRegistryNamespace = "req-namespace",
                                    SchemaVersion = "1.0"
                                },
                                ResponseMessageSchemaReference = new AdrBaseService.MessageSchemaReference
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
                        Error = new AdrBaseService.ConfigError
                        {
                            Code = "stream-error",
                            Message = "stream-error-message"
                        },
                        MessageSchemaReference = new AdrBaseService.MessageSchemaReference
                        {
                            SchemaName = "stream-schema",
                            SchemaRegistryNamespace = "stream-namespace",
                            SchemaVersion = "3.0"
                        }
                    }
                ]
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);

            // Config assertions
            Assert.NotNull(result.Config);
            Assert.NotNull(result.Config.Error);
            Assert.Equal("error-code", result.Config.Error.Code);
            Assert.Equal("error-message", result.Config.Error.Message);
            Assert.Equal("2023-01-01T00:00:00Z", result.Config.LastTransitionTime);
            Assert.Equal((ulong)1, result.Config?.Version);

            // Datasets assertions
            Assert.NotNull(result.Datasets);
            Assert.Single(result.Datasets);
            Assert.Equal("dataset1", result.Datasets[0].Name);
            Assert.NotNull(result.Datasets[0].Error);
            Assert.Equal("dataset-error", result.Datasets[0].Error?.Code);
            Assert.NotNull(result.Datasets[0].MessageSchemaReference);
            Assert.Equal("schema1", result.Datasets[0].MessageSchemaReference?.SchemaName);

            // Events assertions
            Assert.NotNull(result.Events);
            Assert.Single(result.Events);
            Assert.Equal("event1", result.Events[0].Name);
            Assert.NotNull(result.Events[0].MessageSchemaReference);
            Assert.Equal("event-schema", result.Events[0].MessageSchemaReference?.SchemaName);

            // ManagementGroups assertions
            Assert.NotNull(result.ManagementGroups);
            Assert.Single(result.ManagementGroups);
            Assert.Equal("mgmt-group1", result.ManagementGroups[0].Name);
            Assert.NotNull(result.ManagementGroups[0].Actions);
            Assert.Single(result.ManagementGroups[0].Actions!);
            Assert.Equal("action1", result.ManagementGroups[0].Actions?[0].Name);
            Assert.NotNull(result.ManagementGroups[0].Actions?[0].Error);

            // Streams assertions
            Assert.NotNull(result.Streams);
            Assert.Single(result.Streams);
            Assert.Equal("stream1", result.Streams[0].Name);
            Assert.NotNull(result.Streams[0].Error);
            Assert.NotNull(result.Streams[0].MessageSchemaReference);
        }

        [Fact]
        public void Asset_ToModel_ShouldConvertAllProperties()
        {
            // Arrange - Create complete source Asset with all properties
            var source = new AdrBaseService.Asset
            {
                Name = "test-asset",
                Specification = new AdrBaseService.AssetSpecificationSchema
                {
                    AssetTypeRefs = ["test-asset-type"],
                    Description = "test-description",
                    Enabled = true,
                    Manufacturer = "test-manufacturer",
                    Model = "test-model",
                    Uuid = "test-uuid",
                    Version = 1,
                    DisplayName = "Test Asset",
                    DocumentationUri = "https://docs.example.com",
                    HardwareRevision = "v1.2",
                    ManufacturerUri = "https://manufacturer.example.com",
                    ProductCode = "ABC123",
                    SerialNumber = "SN12345",
                    SoftwareRevision = "sw1.3",
                    ExternalAssetId = "ext-id-123",
                    Attributes = new Dictionary<string, string>
                    {
                        ["key1"] = "value1",
                        ["key2"] = "value2"
                    },
                    Datasets =
                    [
                        new AdrBaseService.AssetDatasetSchemaElementSchema
                        {
                            Name = "dataset1",
                            DataSource = "ds-source",
                            TypeRef = "ds-type",
                            DataPoints =
                            [
                                new AdrBaseService.AssetDatasetDataPointSchemaElementSchema
                                {
                                    Name = "datapoint1",
                                    DataSource = "dp-source",
                                    TypeRef = "dp-type",
                                    DataPointConfiguration = "{\"someKey\":\"someValue\"}"
                                }
                            ],
                            Destinations =
                            [
                                new AdrBaseService.AssetDatasetDestinationSchemaElementSchema
                                {
                                    Target = AdrBaseService.DatasetTarget.Mqtt,
                                    Configuration = new AdrBaseService.DestinationConfiguration
                                    {
                                        Key = "config-key",
                                        Path = "config-path",
                                        Topic = "config-topic",
                                        Qos = AdrBaseService.Qos.Qos1,
                                        Retain = AdrBaseService.Retain.Keep,
                                        Ttl = 60
                                    }
                                }
                            ]
                        }
                    ],
                    Events =
                    [
                        new AdrBaseService.AssetEventSchemaElementSchema
                        {
                            Name = "event1",
                            EventNotifier = "event-notifier",
                            EventConfiguration = "{\"someKey\":\"someValue\"}",
                            DataPoints =
                            [
                                new AdrBaseService.AssetEventDataPointSchemaElementSchema
                                {
                                    Name = "event-datapoint1",
                                    DataSource = "event-dp-source",
                                    DataPointConfiguration = "{\"someKey\":\"someValue\"}"
                                }
                            ],
                            Destinations =
                            [
                                new AdrBaseService.AssetEventDestinationSchemaElementSchema
                                {
                                    Target = AdrBaseService.EventStreamTarget.Storage,
                                    Configuration = new AdrBaseService.DestinationConfiguration
                                    {
                                        Key = "event-config-key",
                                        Path = "event-config-path",
                                        Topic = "event-config-topic"
                                    }
                                }
                            ]
                        }
                    ],
                    DefaultDatasetsDestinations =
                    [
                        new AdrBaseService.DefaultDatasetsDestinationsSchemaElementSchema
                        {
                            Target = AdrBaseService.DatasetTarget.Mqtt,
                            Configuration = new AdrBaseService.DestinationConfiguration
                            {
                                Key = "default-config-key",
                                Path = "default-config-path",
                                Topic = "default-config-topic",
                                Qos = AdrBaseService.Qos.Qos1,
                                Retain = AdrBaseService.Retain.Keep,
                                Ttl = 60
                            }
                        }
                    ],
                    DeviceRef = new AdrBaseService.DeviceRefSchema
                    {
                        DeviceName = "device-name",
                        EndpointName = "endpoint-name"
                    },
                    DefaultDatasetsConfiguration = "{\"defaultKey\":\"defaultValue\"}",
                    DefaultEventsConfiguration = "{\"defaultEventKey\":\"defaultEventValue\"}",
                    DefaultManagementGroupsConfiguration = "{\"defaultManagementGroupKey\":\"defaultManagementGroupValue\"}",
                    DiscoveredAssetRefs = ["discovered-asset-ref"]
                },
                Status = new AdrBaseService.AssetStatus
                {
                    Config = new AdrBaseService.AssetConfigStatusSchema
                    {
                        Error = new AdrBaseService.ConfigError
                        {
                            Code = "error-code",
                            Message = "error-message",
                            InnerError = new Dictionary<string, string> { { "inner-error", "inner-error-message" } },
                            Details =
                            [
                                new()
                                {
                                    Code = "detail-code",
                                    Message = "detail-message",
                                    Info = "info",
                                    CorrelationId = "correlation-id"
                                }
                            ]
                        },
                        LastTransitionTime = "2023-01-01T00:00:00Z",
                        Version = 1
                    },
                    Datasets =
                    [
                        new()
                        {
                            Name = "dataset1",
                            Error = new AdrBaseService.ConfigError
                            {
                                Code = "dataset-error",
                                Message = "dataset-error-message"
                            },
                            MessageSchemaReference = new AdrBaseService.MessageSchemaReference
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
                            MessageSchemaReference = new AdrBaseService.MessageSchemaReference
                            {
                                SchemaName = "event-schema",
                                SchemaRegistryNamespace = "event-namespace",
                                SchemaVersion = "2.0"
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
                                    Error = new AdrBaseService.ConfigError
                                    {
                                        Code = "action-error",
                                        Message = "action-error-message"
                                    },
                                    RequestMessageSchemaReference = new AdrBaseService.MessageSchemaReference
                                    {
                                        SchemaName = "req-schema",
                                        SchemaRegistryNamespace = "req-namespace",
                                        SchemaVersion = "1.0"
                                    },
                                    ResponseMessageSchemaReference = new AdrBaseService.MessageSchemaReference
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
                            Error = new AdrBaseService.ConfigError
                            {
                                Code = "stream-error",
                                Message = "stream-error-message"
                            },
                            MessageSchemaReference = new AdrBaseService.MessageSchemaReference
                            {
                                SchemaName = "stream-schema",
                                SchemaRegistryNamespace = "stream-namespace",
                                SchemaVersion = "3.0"
                            }
                        }
                    ]
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test-asset", result.Name);

            // Verify Specification
            Assert.NotNull(result.Specification);
            Assert.Equal("test-description", result.Specification.Description);
            Assert.True(result.Specification.Enabled);
            Assert.Equal("test-manufacturer", result.Specification.Manufacturer);
            Assert.Equal("test-model", result.Specification.Model);
            Assert.Equal("test-uuid", result.Specification.Uuid);
            Assert.Equal((ulong)1, result.Specification.Version);
            Assert.Equal("Test Asset", result.Specification.DisplayName);
            Assert.Equal("https://docs.example.com", result.Specification.DocumentationUri);
            Assert.Equal("v1.2", result.Specification.HardwareRevision);
            Assert.Equal("https://manufacturer.example.com", result.Specification.ManufacturerUri);
            Assert.Equal("ABC123", result.Specification.ProductCode);
            Assert.Equal("SN12345", result.Specification.SerialNumber);
            Assert.Equal("sw1.3", result.Specification.SoftwareRevision);
            Assert.Equal("ext-id-123", result.Specification.ExternalAssetId);
            Assert.Equal("device-name", result.Specification.DeviceRef.DeviceName);
            Assert.Equal("endpoint-name", result.Specification.DeviceRef.EndpointName);

            // Verify AssetTypeRefs
            Assert.NotNull(result.Specification.AssetTypeRefs);
            Assert.Single(result.Specification.AssetTypeRefs);
            Assert.Equal("test-asset-type", result.Specification.AssetTypeRefs[0]);

            // Verify Attributes
            Assert.NotNull(result.Specification.Attributes);
            Assert.Equal(2, result.Specification.Attributes.Count);
            Assert.Equal("value1", result.Specification.Attributes["key1"]);
            Assert.Equal("value2", result.Specification.Attributes["key2"]);

            // Verify Datasets
            Assert.NotNull(result.Specification.Datasets);
            Assert.Single(result.Specification.Datasets);
            Assert.Equal("dataset1", result.Specification.Datasets[0].Name);
            Assert.Equal("ds-source", result.Specification.Datasets[0].DataSource);
            Assert.Equal("ds-type", result.Specification.Datasets[0].TypeRef);

            // Verify Dataset DataPoints
            Assert.NotNull(result.Specification.Datasets[0].DataPoints);
            Assert.Single(result.Specification.Datasets[0].DataPoints!);
            Assert.Equal("datapoint1", result.Specification.Datasets[0].DataPoints![0].Name);
            Assert.Equal("dp-source", result.Specification.Datasets[0].DataPoints![0].DataSource);
            Assert.Equal("dp-type", result.Specification.Datasets[0].DataPoints![0].TypeRef);
            string actualJson = JsonSerializer.Serialize(result.Specification.Datasets[0].DataPoints![0].DataPointConfiguration);
            string expectedJson = JsonSerializer.Serialize(JsonDocument.Parse("{\"someKey\":\"someValue\"}"));
            Assert.Equal(expectedJson, actualJson);

            // Verify Dataset Destinations
            Assert.NotNull(result.Specification.Datasets[0].Destinations);
            Assert.Single(result.Specification.Datasets[0].Destinations!);
            Assert.Equal(DatasetTarget.Mqtt, result.Specification.Datasets[0].Destinations![0].Target);
            Assert.NotNull(result.Specification.Datasets[0].Destinations![0].Configuration);
            Assert.Equal("config-key", result.Specification.Datasets[0].Destinations![0].Configuration.Key);
            Assert.Equal("config-path", result.Specification.Datasets[0].Destinations![0].Configuration.Path);
            Assert.Equal("config-topic", result.Specification.Datasets[0].Destinations![0].Configuration.Topic);
            Assert.Equal(QoS.Qos1, result.Specification.Datasets[0].Destinations![0].Configuration.Qos);
            Assert.Equal(Retain.Keep, result.Specification.Datasets[0].Destinations![0].Configuration.Retain);
            Assert.Equal((ulong)60, result.Specification.Datasets[0].Destinations![0].Configuration.Ttl);

            // Verify Events
            Assert.NotNull(result.Specification.Events);
            Assert.Single(result.Specification.Events);
            Assert.Equal("event1", result.Specification.Events[0].Name);
            Assert.Equal("event-notifier", result.Specification.Events[0].EventNotifier);

            // Verify Event DataPoints
            Assert.NotNull(result.Specification.Events[0].DataPoints);
            Assert.Single(result.Specification.Events[0].DataPoints!);
            Assert.Equal("event-datapoint1", result.Specification.Events[0].DataPoints![0].Name);
            Assert.Equal("event-dp-source", result.Specification.Events[0].DataPoints![0].DataSource);

            // Verify Event Destinations
            Assert.NotNull(result.Specification.Events[0].Destinations);
            Assert.Single(result.Specification.Events[0].Destinations!);
            Assert.Equal(EventStreamTarget.Storage, result.Specification.Events[0].Destinations![0].Target);

            // Verify DefaultDatasetsDestinations
            Assert.NotNull(result.Specification.DefaultDatasetsDestinations);
            Assert.Single(result.Specification.DefaultDatasetsDestinations);
            Assert.Equal(DatasetTarget.Mqtt, result.Specification.DefaultDatasetsDestinations[0].Target);
            Assert.NotNull(result.Specification.DefaultDatasetsDestinations[0].Configuration);

            // Verify Status
            Assert.NotNull(result.Status);
            Assert.NotNull(result.Status.Config);
            Assert.Equal("error-code", result.Status.Config.Error!.Code);
            Assert.Equal("error-message", result.Status.Config.Error.Message);
            Assert.Equal("2023-01-01T00:00:00Z", result.Status.Config.LastTransitionTime);
            Assert.Equal((ulong)1, result.Status.Config.Version);
            Assert.NotNull(result.Status.Config.Error.InnerError);
            Assert.Equal("inner-error-message", result.Status.Config.Error.InnerError["inner-error"]);
            Assert.NotNull(result.Status.Config.Error.Details);
            Assert.Single(result.Status.Config.Error.Details);
            Assert.Equal("detail-code", result.Status.Config.Error.Details[0].Code);
            Assert.Equal("detail-message", result.Status.Config.Error.Details[0].Message);
            Assert.Equal("info", result.Status.Config.Error.Details[0].Info);
            Assert.Equal("correlation-id", result.Status.Config.Error.Details[0].CorrelationId);

            // Verify Datasets in Status
            Assert.NotNull(result.Status.Datasets);
            Assert.Single(result.Status.Datasets);
            Assert.Equal("dataset1", result.Status.Datasets[0].Name);
            Assert.NotNull(result.Status.Datasets[0].Error);
            Assert.Equal("dataset-error", result.Status.Datasets[0].Error!.Code);
            Assert.Equal("dataset-error-message", result.Status.Datasets[0].Error!.Message);
            Assert.NotNull(result.Status.Datasets[0].MessageSchemaReference);
            Assert.Equal("schema1", result.Status.Datasets[0].MessageSchemaReference!.SchemaName);
            Assert.Equal("namespace1", result.Status.Datasets[0].MessageSchemaReference!.SchemaRegistryNamespace);
            Assert.Equal("1.0", result.Status.Datasets[0].MessageSchemaReference!.SchemaVersion);

            // Verify Events in Status
            Assert.NotNull(result.Status.Events);
            Assert.Single(result.Status.Events);
            Assert.Equal("event1", result.Status.Events[0].Name);
            Assert.NotNull(result.Status.Events[0].MessageSchemaReference);
            Assert.Equal("event-schema", result.Status.Events[0].MessageSchemaReference!.SchemaName);
            Assert.Equal("event-namespace", result.Status.Events[0].MessageSchemaReference!.SchemaRegistryNamespace);
            Assert.Equal("2.0", result.Status.Events[0].MessageSchemaReference!.SchemaVersion);
            // Verify ManagementGroups in Status
            Assert.NotNull(result.Status.ManagementGroups);
            Assert.Single(result.Status.ManagementGroups);
            Assert.Equal("mgmt-group1", result.Status.ManagementGroups[0].Name);
            Assert.NotNull(result.Status.ManagementGroups[0].Actions);
            Assert.Single(result.Status.ManagementGroups[0].Actions!);
            Assert.Equal("action1", result.Status.ManagementGroups[0].Actions?[0].Name);
            Assert.NotNull(result.Status.ManagementGroups[0].Actions?[0].Error);
            Assert.Equal("action-error", result.Status.ManagementGroups[0].Actions?[0].Error?.Code);
            Assert.Equal("action-error-message", result.Status.ManagementGroups[0].Actions?[0].Error?.Message);
            Assert.NotNull(result.Status.ManagementGroups[0].Actions?[0].RequestMessageSchemaReference);
            Assert.Equal("req-schema", result.Status.ManagementGroups[0].Actions?[0].RequestMessageSchemaReference?.SchemaName);
            Assert.Equal("req-namespace", result.Status.ManagementGroups[0].Actions?[0].RequestMessageSchemaReference?.SchemaRegistryNamespace);
            Assert.Equal("1.0", result.Status.ManagementGroups[0].Actions?[0].RequestMessageSchemaReference?.SchemaVersion);
            Assert.NotNull(result.Status.ManagementGroups[0].Actions?[0].ResponseMessageSchemaReference);
            Assert.Equal("resp-schema", result.Status.ManagementGroups[0].Actions?[0].ResponseMessageSchemaReference?.SchemaName);
            Assert.Equal("resp-namespace", result.Status.ManagementGroups[0].Actions?[0].ResponseMessageSchemaReference?.SchemaRegistryNamespace);
            Assert.Equal("1.0", result.Status.ManagementGroups[0].Actions?[0].ResponseMessageSchemaReference?.SchemaVersion);
            // Verify Streams in Status
            Assert.NotNull(result.Status.Streams);
            Assert.Single(result.Status.Streams);
            Assert.Equal("stream1", result.Status.Streams[0].Name);
            Assert.NotNull(result.Status.Streams[0].Error);
            Assert.Equal("stream-error", result.Status.Streams[0].Error?.Code);
            Assert.Equal("stream-error-message", result.Status.Streams[0].Error?.Message);
            Assert.NotNull(result.Status.Streams[0].MessageSchemaReference);
            Assert.Equal("stream-schema", result.Status.Streams[0].MessageSchemaReference?.SchemaName);
            Assert.Equal("stream-namespace", result.Status.Streams[0].MessageSchemaReference?.SchemaRegistryNamespace);
            Assert.Equal("3.0", result.Status.Streams[0].MessageSchemaReference?.SchemaVersion);

            // Verify DiscoveredAssetRefs
            Assert.NotNull(result.Specification.DiscoveredAssetRefs);
            Assert.Single(result.Specification.DiscoveredAssetRefs);
            Assert.Equal("discovered-asset-ref", result.Specification.DiscoveredAssetRefs[0]);

            // Verify DefaultDatasetsConfiguration
            Assert.NotNull(result.Specification.DefaultDatasetsConfiguration);
            actualJson = JsonSerializer.Serialize(result.Specification.DefaultDatasetsConfiguration);
            expectedJson = JsonSerializer.Serialize(JsonDocument.Parse("{\"defaultKey\":\"defaultValue\"}"));
            Assert.Equal(expectedJson, actualJson);
            // Verify DefaultEventsConfiguration
            Assert.NotNull(result.Specification.DefaultEventsConfiguration);
            actualJson = JsonSerializer.Serialize(result.Specification.DefaultEventsConfiguration);
            expectedJson = JsonSerializer.Serialize(JsonDocument.Parse("{\"defaultEventKey\":\"defaultEventValue\"}"));
            Assert.Equal(expectedJson, actualJson);

            // Verify DefaultDatasetsDestinations
            Assert.NotNull(result.Specification.DefaultDatasetsDestinations);
            Assert.Single(result.Specification.DefaultDatasetsDestinations);
            Assert.Equal(DatasetTarget.Mqtt, result.Specification.DefaultDatasetsDestinations[0].Target);
            Assert.NotNull(result.Specification.DefaultDatasetsDestinations[0].Configuration);
            Assert.Equal("default-config-key", result.Specification.DefaultDatasetsDestinations[0].Configuration.Key);
            Assert.Equal("default-config-path", result.Specification.DefaultDatasetsDestinations[0].Configuration.Path);
            Assert.Equal("default-config-topic", result.Specification.DefaultDatasetsDestinations[0].Configuration.Topic);
            Assert.Equal(QoS.Qos1, result.Specification.DefaultDatasetsDestinations[0].Configuration.Qos);
            Assert.Equal(Retain.Keep, result.Specification.DefaultDatasetsDestinations[0].Configuration.Retain);
            Assert.Equal((ulong)60, result.Specification.DefaultDatasetsDestinations[0].Configuration.Ttl);

            Assert.NotNull(result.Specification.DefaultManagementGroupsConfiguration);
            actualJson = JsonSerializer.Serialize(result.Specification.DefaultManagementGroupsConfiguration);
            expectedJson = source.Specification.DefaultManagementGroupsConfiguration;
            Assert.Equal(expectedJson, actualJson);
        }

        [Fact]
        public void CreateDetectedAssetResponse_ToModel_ShouldConvertStatus()
        {
            // Arrange - Test with each possible status value
            var sourceSuccess = new AdrBaseService.CreateDetectedAssetResponseSchema
            {
                Status =  AdrBaseService.DetectedAssetResponseStatusSchema.Created
            };

            var sourceFailed = new AdrBaseService.CreateDetectedAssetResponseSchema
            {
                Status = AdrBaseService.DetectedAssetResponseStatusSchema.Failed
            };

            var sourceDuplicate = new AdrBaseService.CreateDetectedAssetResponseSchema
            {
                Status = AdrBaseService.DetectedAssetResponseStatusSchema.Duplicate
            };

            // Act
            var resultCreated = sourceSuccess.ToModel();
            var resultFailed = sourceFailed.ToModel();
            var resultDuplicate = sourceDuplicate.ToModel();

            // Assert
            Assert.Equal(DetectedAssetResponseStatus.Created, resultCreated.Status);
            Assert.Equal(DetectedAssetResponseStatus.Failed, resultFailed.Status);
            Assert.Equal(DetectedAssetResponseStatus.Duplicate, resultDuplicate.Status);

            // Verify the underlying integer values match
            Assert.Equal((int)AdrBaseService.DetectedAssetResponseStatusSchema.Created, (int)resultCreated.Status);
            Assert.Equal((int)AdrBaseService.DetectedAssetResponseStatusSchema.Failed, (int)resultFailed.Status);
            Assert.Equal((int)AdrBaseService.DetectedAssetResponseStatusSchema.Duplicate, (int)resultDuplicate.Status);
        }

        [Fact]
        public void Device_ToModel_ShouldConvertAllProperties()
        {
            // Arrange - Create complete source Device with all properties
            var source = new AdrBaseService.Device
            {
                Name = "test-device",
                Specification = new AdrBaseService.DeviceSpecificationSchema
                {
                    Manufacturer = "test-manufacturer",
                    Model = "test-model",
                    Uuid = "test-uuid",
                    Version = 1,
                    Enabled = true,
                    DiscoveredDeviceRef = "discovered-device-ref",
                    ExternalDeviceId = "external-device-id",
                    LastTransitionTime = "2023-01-01T00:00:00Z",
                    OperatingSystemVersion = "1.2.3",
                    Attributes = new Dictionary<string, string>
                    {
                        ["key1"] = "value1",
                        ["key2"] = "value2"
                    },
                    Endpoints = new AdrBaseService.DeviceEndpointSchema
                    {
                        Inbound = new Dictionary<string, AdrBaseService.InboundSchemaMapValueSchema>
                        {
                            ["mqtt-endpoint"] = new()
                            {
                                Address = "mqtt://device:1883",
                                Version = "1.0",
                                EndpointType = "mqtt",
                                AdditionalConfiguration = "{\"someKey\":\"someValue\"}",
                                Authentication = new AdrBaseService.AuthenticationSchema
                                {
                                    Method = AdrBaseService.MethodSchema.Certificate,
                                    X509credentials = new AdrBaseService.X509credentialsSchema
                                    {
                                        CertificateSecretName = "certificate-secret"
                                    }
                                },
                                TrustSettings = new AdrBaseService.TrustSettingsSchema
                                {
                                    IssuerList = "issuer1",
                                    TrustList = "trust1"
                                }
                            },
                            ["http-endpoint"] = new()
                            {
                                Address = "http://device:8080",
                                Version = "2.0",
                                EndpointType = "http",
                                Authentication = new AdrBaseService.AuthenticationSchema
                                {
                                    Method = AdrBaseService.MethodSchema.UsernamePassword,
                                    UsernamePasswordCredentials = new AdrBaseService.UsernamePasswordCredentialsSchema
                                    {
                                        UsernameSecretName = "username-secret",
                                        PasswordSecretName = "password-secret"
                                    }
                                }
                            }
                        },
                        Outbound = new AdrBaseService.OutboundSchema
                        {
                            Assigned = new Dictionary<string, AdrBaseService.DeviceOutboundEndpoint>{
                                ["mqtt-endpoint"] = new()
                                {
                                    Address = "mqtt://device:1883",
                                    EndpointType = "mqtt"
                                }
                            },
                            Unassigned = new Dictionary<string, AdrBaseService.DeviceOutboundEndpoint>
                            {
                                ["http-endpoint"] = new()
                                {
                                    Address = "http://device:8080",
                                    EndpointType = "http"
                                }
                            }
                        }
                    }
                },
                Status = new AdrBaseService.DeviceStatus
                {
                    Config = new AdrBaseService.DeviceStatusConfigSchema
                    {
                        LastTransitionTime = "2023-01-02T00:00:00Z",
                        Version = 2,
                        Error = new AdrBaseService.ConfigError
                        {
                            Code = "config-error-code",
                            Message = "config-error-message",
                            InnerError = new Dictionary<string, string>
                            {
                                ["inner-key"] = "inner-value"
                            },
                            Details = new List<AdrBaseService.ConfigErrorDetailsSchema>
                            {
                                new()
                                {
                                    Code = "detail-code",
                                    Message = "detail-message",
                                    Info = "detail-info",
                                    CorrelationId = "correlation-id"
                                }
                            }
                        }
                    },
                    Endpoints = new AdrBaseService.DeviceStatusEndpointSchema
                    {
                        Inbound = new Dictionary<string, AdrBaseService.DeviceStatusInboundEndpointSchemaMapValueSchema>
                        {
                            ["mqtt-endpoint"] = new()
                            {
                                Error = new AdrBaseService.ConfigError
                                {
                                    Code = "mqtt-error-code",
                                    Message = "mqtt-error-message"
                                }
                            },
                            ["http-endpoint"] = new()
                            {
                                Error = new AdrBaseService.ConfigError
                                {
                                    Code = "http-error-code",
                                    Message = "http-error-message"
                                }
                            }
                        }
                    }
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);

            // Verify top-level Device properties
            Assert.Equal("test-device", result.Name);

            // Verify DeviceSpecification
            Assert.NotNull(result.Specification);
            Assert.Equal("test-manufacturer", result.Specification.Manufacturer);
            Assert.Equal("test-model", result.Specification.Model);
            Assert.Equal("test-uuid", result.Specification.Uuid);
            Assert.Equal((ulong)1, result.Specification.Version);
            Assert.True(result.Specification.Enabled);
            Assert.Equal("discovered-device-ref", result.Specification.DiscoveredDeviceRef);
            Assert.Equal("external-device-id", result.Specification.ExternalDeviceId);
            Assert.Equal("2023-01-01T00:00:00Z", result.Specification.LastTransitionTime);
            Assert.Equal("1.2.3", result.Specification.OperatingSystemVersion);

            // Verify Attributes
            Assert.NotNull(result.Specification.Attributes);
            Assert.Equal(2, result.Specification.Attributes.Count);
            Assert.Equal("value1", result.Specification.Attributes["key1"]);
            Assert.Equal("value2", result.Specification.Attributes["key2"]);

            // Verify Endpoints
            Assert.NotNull(result.Specification.Endpoints);
            Assert.NotNull(result.Specification.Endpoints.Inbound);
            Assert.Equal(2, result.Specification.Endpoints.Inbound.Count);

            // Verify first endpoint (MQTT)
            Assert.True(result.Specification.Endpoints.Inbound.ContainsKey("mqtt-endpoint"));
            var mqttEndpoint = result.Specification.Endpoints.Inbound["mqtt-endpoint"];
            Assert.Equal("mqtt://device:1883", mqttEndpoint.Address);
            Assert.Equal("1.0", mqttEndpoint.Version);
            Assert.Equal("mqtt", mqttEndpoint.EndpointType);
            Assert.NotNull(mqttEndpoint.AdditionalConfiguration);
            string actualJson = JsonSerializer.Serialize(mqttEndpoint.AdditionalConfiguration);
            string expectedJson = source.Specification.Endpoints.Inbound["mqtt-endpoint"].AdditionalConfiguration!;
            Assert.Equal(expectedJson, actualJson);

            // Verify MQTT endpoint authentication
            Assert.NotNull(mqttEndpoint.Authentication);
            Assert.Equal(Method.Certificate, mqttEndpoint.Authentication.Method);
            Assert.NotNull(mqttEndpoint.Authentication.X509Credentials);
            Assert.Equal("certificate-secret", mqttEndpoint.Authentication.X509Credentials.CertificateSecretName);

            // Verify MQTT endpoint trust settings
            Assert.NotNull(mqttEndpoint.TrustSettings);
            Assert.NotNull(mqttEndpoint.TrustSettings.IssuerList);
            Assert.Equal("issuer1", mqttEndpoint.TrustSettings.IssuerList);
            Assert.NotNull(mqttEndpoint.TrustSettings.TrustList);
            Assert.Equal("trust1", mqttEndpoint.TrustSettings.TrustList);

            // Verify second endpoint (HTTP)
            Assert.True(result.Specification.Endpoints.Inbound.ContainsKey("http-endpoint"));
            var httpEndpoint = result.Specification.Endpoints.Inbound["http-endpoint"];
            Assert.Equal("http://device:8080", httpEndpoint.Address);
            Assert.Equal("2.0", httpEndpoint.Version);
            Assert.Equal("http", httpEndpoint.EndpointType);

            // Verify HTTP endpoint authentication
            Assert.NotNull(httpEndpoint.Authentication);
            Assert.Equal(Method.UsernamePassword, httpEndpoint.Authentication.Method);
            Assert.NotNull(httpEndpoint.Authentication.UsernamePasswordCredentials);
            Assert.Equal("username-secret", httpEndpoint.Authentication.UsernamePasswordCredentials.UsernameSecretName);
            Assert.Equal("password-secret", httpEndpoint.Authentication.UsernamePasswordCredentials.PasswordSecretName);

            // Verify Outbound endpoints
            Assert.NotNull(result.Specification.Endpoints.Outbound);
            Assert.NotNull(result.Specification.Endpoints.Outbound.Assigned);
            Assert.NotNull(result.Specification.Endpoints.Outbound.Unassigned);

            // Verify Assigned outbound endpoints
            Assert.Single(result.Specification.Endpoints.Outbound.Assigned);
            Assert.True(result.Specification.Endpoints.Outbound.Assigned.ContainsKey("mqtt-endpoint"));
            var mqttOutbound = result.Specification.Endpoints.Outbound.Assigned["mqtt-endpoint"];
            Assert.Equal("mqtt://device:1883", mqttOutbound.Address);
            Assert.Equal("mqtt", mqttOutbound.EndpointType);

            // Verify Unassigned outbound endpoints
            Assert.Single(result.Specification.Endpoints.Outbound.Unassigned);
            Assert.True(result.Specification.Endpoints.Outbound.Unassigned.ContainsKey("http-endpoint"));
            var httpOutbound = result.Specification.Endpoints.Outbound.Unassigned["http-endpoint"];
            Assert.Equal("http://device:8080", httpOutbound.Address);
            Assert.Equal("http", httpOutbound.EndpointType);

            // Verify DeviceStatus
            Assert.NotNull(result.Status);
            Assert.NotNull(result.Status.Config);
            Assert.Equal("2023-01-02T00:00:00Z", result.Status.Config.LastTransitionTime);
            Assert.Equal((ulong)2, result.Status.Config.Version);

            // Verify Error information
            Assert.NotNull(result.Status.Config.Error);
            Assert.Equal("config-error-code", result.Status.Config.Error.Code);
            Assert.Equal("config-error-message", result.Status.Config.Error.Message);
            Assert.NotNull(result.Status.Config.Error.InnerError);
            Assert.Equal("inner-value", result.Status.Config.Error.InnerError["inner-key"]);

            // Verify Error Details
            Assert.NotNull(result.Status.Config.Error.Details);
            Assert.Single(result.Status.Config.Error.Details);
            Assert.Equal("detail-code", result.Status.Config.Error.Details[0].Code);
            Assert.Equal("detail-message", result.Status.Config.Error.Details[0].Message);
            Assert.Equal("detail-info", result.Status.Config.Error.Details[0].Info);
            Assert.Equal("correlation-id", result.Status.Config.Error.Details[0].CorrelationId);

            // Verify Status Endpoints
            Assert.NotNull(result.Status.Endpoints);
            Assert.NotNull(result.Status.Endpoints.Inbound);
            Assert.Equal(2, result.Status.Endpoints.Inbound.Count);

            // Verify first status endpoint
            Assert.True(result.Status.Endpoints.Inbound.ContainsKey("mqtt-endpoint"));
            var mqttStatusEndpoint = result.Status.Endpoints.Inbound["mqtt-endpoint"];
            Assert.NotNull(mqttStatusEndpoint.Error);
            Assert.Equal("mqtt-error-code", mqttStatusEndpoint.Error.Code);

            // Verify second status endpoint
            Assert.True(result.Status.Endpoints.Inbound.ContainsKey("http-endpoint"));
            var httpStatusEndpoint = result.Status.Endpoints.Inbound["http-endpoint"];
            Assert.NotNull(httpStatusEndpoint.Error);
            Assert.Equal("http-error-code", httpStatusEndpoint.Error.Code);
        }

        [Fact]
        public void CreateDiscoveredAssetEndpointProfileResponse_ToModel_ShouldConvertStatus()
        {
            // Arrange - Test with each possible status value
            var sourceCreated = new CreateDiscoveredAssetEndpointProfileResponseSchema
            {
                Status = DiscoveredAssetEndpointProfileResponseStatusSchema.Created
            };

            var sourceFailed = new CreateDiscoveredAssetEndpointProfileResponseSchema
            {
                Status = DiscoveredAssetEndpointProfileResponseStatusSchema.Failed
            };

            var sourceDuplicate = new CreateDiscoveredAssetEndpointProfileResponseSchema
            {
                Status = DiscoveredAssetEndpointProfileResponseStatusSchema.Duplicate
            };

            // Act
            var resultCreated = sourceCreated.ToModel();
            var resultFailed = sourceFailed.ToModel();
            var resultDuplicate = sourceDuplicate.ToModel();

            // Assert
            Assert.Equal(DiscoveredAssetEndpointProfileResponseStatus.Created, resultCreated.Status);
            Assert.Equal(DiscoveredAssetEndpointProfileResponseStatus.Failed, resultFailed.Status);
            Assert.Equal(DiscoveredAssetEndpointProfileResponseStatus.Duplicate, resultDuplicate.Status);

            // Verify the underlying integer values match
            Assert.Equal((int)DiscoveredAssetEndpointProfileResponseStatusSchema.Created, (int)resultCreated.Status);
            Assert.Equal((int)DiscoveredAssetEndpointProfileResponseStatusSchema.Failed, (int)resultFailed.Status);
            Assert.Equal((int)DiscoveredAssetEndpointProfileResponseStatusSchema.Duplicate, (int)resultDuplicate.Status);
        }

        [Fact]
        public void NotificationPreferenceResponse_ToModel_ShouldConvert()
        {
            // Arrange
            var sourceAccepted = AdrBaseService.NotificationPreferenceResponse.Accepted;
            var sourceFailed = AdrBaseService.NotificationPreferenceResponse.Failed;

            // Act
            var resultAccepted = sourceAccepted.ToModel();
            var resultFailed = sourceFailed.ToModel();

            // Assert
            Assert.Equal(NotificationResponse.Accepted, resultAccepted);
            Assert.Equal(NotificationResponse.Failed, resultFailed);
        }
    }
*/
}
