using System.Text.Json;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.DeviceDiscoveryService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Azure.Iot.Operations.Services.StateStore;
using Xunit;
using AdrBaseService = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;

namespace Azure.Iot.Operations.Services.UnitTests.AssetAndDeviceRegistry
{
    public class ModelsConverterTests
    {
        [Fact]
        public void AssetStatus_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.AssetStatus
            {
                Config = new AdrBaseService.AssetConfigStatusSchema
                {
                    Version = 1,
                    LastTransitionTime = "2023-01-01T00:00:00Z"
                },
                Datasets = new List<AdrBaseService.AssetDatasetEventStreamStatus>
                {
                    new AdrBaseService.AssetDatasetEventStreamStatus
                    {
                        Name = "TestDataset",
                        MessageSchemaReference = new AdrBaseService.MessageSchemaReference
                        {
                            SchemaName = "TestSchema",
                            SchemaRegistryNamespace = "TestNamespace",
                            SchemaVersion = "1.0"
                        }
                    }
                },
                Events = new List<AdrBaseService.AssetDatasetEventStreamStatus>
                {
                    new AdrBaseService.AssetDatasetEventStreamStatus
                    {
                        Name = "TestEvent"
                    }
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Config);
            Assert.Equal(1, result.Config.Version);
            Assert.Equal("2023-01-01T00:00:00Z", result.Config.LastTransitionTime);
            Assert.NotNull(result.Datasets);
            Assert.Single(result.Datasets);
            Assert.Equal("TestDataset", result.Datasets[0].Name);
            Assert.NotNull(result.Datasets[0].MessageSchemaReference);
            Assert.Equal("TestSchema", result.Datasets[0].MessageSchemaReference.SchemaName);
            Assert.NotNull(result.Events);
            Assert.Single(result.Events);
            Assert.Equal("TestEvent", result.Events[0].Name);
        }

        [Fact]
        public void Asset_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.Asset
            {
                Name = "TestAsset",
                Specification = new AdrBaseService.AssetSpecificationSchema
                {
                    DisplayName = "Test Display Name",
                    Description = "Test Description",
                    Enabled = true,
                    DeviceRef = new AdrBaseService.AssetDeviceRef
                    {
                        DeviceName = "TestDevice",
                        EndpointName = "TestEndpoint"
                    },
                    Attributes = new Dictionary<string, string> { { "key1", "value1" } }
                },
                Status = new AdrBaseService.AssetStatus
                {
                    Config = new AdrBaseService.AssetConfigStatusSchema
                    {
                        Version = 1
                    }
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestAsset", result.Name);
            Assert.NotNull(result.Specification);
            Assert.Equal("Test Display Name", result.Specification.DisplayName);
            Assert.Equal("Test Description", result.Specification.Description);
            Assert.True(result.Specification.Enabled);
            Assert.NotNull(result.Specification.DeviceRef);
            Assert.Equal("TestDevice", result.Specification.DeviceRef.DeviceName);
            Assert.NotNull(result.Status);
            Assert.NotNull(result.Status.Config);
            Assert.Equal(1, result.Status.Config.Version);
        }

        [Fact]
        public void CreateDetectedAssetResponse_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.DiscoveredAssetResponseSchema
            {
                DiscoveryId = "test-discovery-id",
                Version = "1.0"
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test-discovery-id", result.DiscoveryId);
            Assert.Equal("1.0", result.Version);
        }

        [Fact]
        public void AkriServiceError_AdrBaseService_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.AkriServiceError
            {
                Code = "404",
                Message = "Not Found",
                Timestamp = "2023-01-01T00:00:00Z"
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("404", result.Code);
            Assert.Equal("Not Found", result.Message);
            Assert.Equal("2023-01-01T00:00:00Z", result.Timestamp);
        }

        [Fact]
        public void AkriServiceError_DeviceDiscoveryService_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new DeviceDiscoveryService.AkriServiceError
            {
                Code = "500",
                Message = "Internal Server Error",
                Timestamp = "2023-01-01T00:00:00Z"
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("500", result.Code);
            Assert.Equal("Internal Server Error", result.Message);
            Assert.Equal("2023-01-01T00:00:00Z", result.Timestamp);
        }

        [Fact]
        public void Device_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.Device
            {
                Name = "TestDevice",
                Specification = new AdrBaseService.DeviceSpecificationSchema
                {
                    Enabled = true,
                    Manufacturer = "Test Manufacturer",
                    Model = "Test Model",
                    Uuid = "test-uuid",
                    Attributes = new Dictionary<string, string> { { "attr1", "value1" } }
                },
                Status = new AdrBaseService.DeviceStatus
                {
                    Config = new AdrBaseService.DeviceStatusConfigSchema
                    {
                        Version = 2,
                        LastTransitionTime = "2023-01-01T00:00:00Z"
                    }
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestDevice", result.Name);
            Assert.NotNull(result.Specification);
            Assert.True(result.Specification.Enabled);
            Assert.Equal("Test Manufacturer", result.Specification.Manufacturer);
            Assert.Equal("Test Model", result.Specification.Model);
            Assert.Equal("test-uuid", result.Specification.Uuid);
            Assert.NotNull(result.Status);
            Assert.NotNull(result.Status.Config);
            Assert.Equal(2, result.Status.Config.Version);
        }

        [Fact]
        public void NotificationResponse_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = NotificationPreferenceResponse.Enabled;

            // Act
            var result = source.ToModel();

            // Assert
            Assert.Equal(NotificationResponse.Enabled, result);
        }

        [Fact]
        public void MessageSchemaReference_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.MessageSchemaReference
            {
                SchemaName = "TestSchema",
                SchemaRegistryNamespace = "TestNamespace",
                SchemaVersion = "1.0"
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestSchema", result.SchemaName);
            Assert.Equal("TestNamespace", result.SchemaRegistryNamespace);
            Assert.Equal("1.0", result.SchemaVersion);
        }

        [Fact]
        public void AssetSpecification_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.AssetSpecificationSchema
            {
                DisplayName = "Test Asset",
                Description = "Test Description",
                Enabled = true,
                DeviceRef = new AdrBaseService.AssetDeviceRef
                {
                    DeviceName = "TestDevice",
                    EndpointName = "TestEndpoint"
                },
                Attributes = new Dictionary<string, string> { { "key1", "value1" } },
                DefaultDatasetsConfiguration = "{ \"test\": \"config\" }",
                DefaultEventsConfiguration = "{ \"event\": \"config\" }",
                Manufacturer = "Test Manufacturer",
                Model = "Test Model",
                Version = 1
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Asset", result.DisplayName);
            Assert.Equal("Test Description", result.Description);
            Assert.True(result.Enabled);
            Assert.NotNull(result.DeviceRef);
            Assert.Equal("TestDevice", result.DeviceRef.DeviceName);
            Assert.Equal("TestEndpoint", result.DeviceRef.EndpointName);
            Assert.NotNull(result.Attributes);
            Assert.Equal("value1", result.Attributes["key1"]);
            Assert.NotNull(result.DefaultDatasetsConfiguration);
            Assert.NotNull(result.DefaultEventsConfiguration);
            Assert.Equal("Test Manufacturer", result.Manufacturer);
            Assert.Equal("Test Model", result.Model);
            Assert.Equal(1, result.Version);
        }

        [Fact]
        public void AssetDatasetSchemaElement_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.AssetDatasetSchemaElementSchema
            {
                Name = "TestDataset",
                DataSource = "TestDataSource",
                TypeRef = "TestTypeRef",
                DatasetConfiguration = "{ \"config\": \"value\" }",
                DataPoints = new List<AdrBaseService.AssetDatasetDataPointSchemaElementSchema>
                {
                    new AdrBaseService.AssetDatasetDataPointSchemaElementSchema
                    {
                        Name = "TestDataPoint",
                        DataSource = "TestDataPointSource"
                    }
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestDataset", result.Name);
            Assert.Equal("TestDataSource", result.DataSource);
            Assert.Equal("TestTypeRef", result.TypeRef);
            Assert.NotNull(result.DatasetConfiguration);
            Assert.NotNull(result.DataPoints);
            Assert.Single(result.DataPoints);
            Assert.Equal("TestDataPoint", result.DataPoints[0].Name);
        }

        [Fact]
        public void AssetDatasetDataPointSchemaElement_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.AssetDatasetDataPointSchemaElementSchema
            {
                Name = "TestDataPoint",
                DataSource = "TestDataSource",
                TypeRef = "TestTypeRef",
                DataPointConfiguration = "{ \"point\": \"config\" }"
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestDataPoint", result.Name);
            Assert.Equal("TestDataSource", result.DataSource);
            Assert.Equal("TestTypeRef", result.TypeRef);
            Assert.NotNull(result.DataPointConfiguration);
        }

        [Fact]
        public void AssetEventSchemaElement_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.AssetEventSchemaElementSchema
            {
                Name = "TestEvent",
                EventNotifier = "TestNotifier",
                TypeRef = "TestTypeRef",
                EventConfiguration = "{ \"event\": \"config\" }",
                DataPoints = new List<AdrBaseService.AssetEventDataPointSchemaElementSchema>
                {
                    new AdrBaseService.AssetEventDataPointSchemaElementSchema
                    {
                        Name = "TestEventDataPoint",
                        DataSource = "TestEventDataSource"
                    }
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestEvent", result.Name);
            Assert.Equal("TestNotifier", result.EventNotifier);
            Assert.Equal("TestTypeRef", result.TypeRef);
            Assert.NotNull(result.EventConfiguration);
            Assert.NotNull(result.DataPoints);
            Assert.Single(result.DataPoints);
            Assert.Equal("TestEventDataPoint", result.DataPoints[0].Name);
        }

        [Fact]
        public void AssetStream_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.AssetStreamSchemaElementSchema
            {
                Name = "TestStream",
                TypeRef = "TestTypeRef",
                StreamConfiguration = "{ \"stream\": \"config\" }"
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestStream", result.Name);
            Assert.Equal("TestTypeRef", result.TypeRef);
            Assert.NotNull(result.StreamConfiguration);
        }

        [Fact]
        public void AssetManagementGroup_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.AssetManagementGroupSchemaElementSchema
            {
                Name = "TestManagementGroup",
                TypeRef = "TestTypeRef",
                DefaultTimeOutInSeconds = 30,
                DefaultTopic = "test/topic",
                ManagementGroupConfiguration = "{ \"mgmt\": \"config\" }",
                Actions = new List<AdrBaseService.AssetManagementGroupActionSchemaElementSchema>
                {
                    new AdrBaseService.AssetManagementGroupActionSchemaElementSchema
                    {
                        Name = "TestAction",
                        ActionType = AdrBaseService.AssetManagementGroupActionType.Write
                    }
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestManagementGroup", result.Name);
            Assert.Equal("TestTypeRef", result.TypeRef);
            Assert.Equal(30, result.DefaultTimeOutInSeconds);
            Assert.Equal("test/topic", result.DefaultTopic);
            Assert.NotNull(result.ManagementGroupConfiguration);
            Assert.NotNull(result.Actions);
            Assert.Single(result.Actions);
            Assert.Equal("TestAction", result.Actions[0].Name);
        }

        [Fact]
        public void AssetManagementGroupAction_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.AssetManagementGroupActionSchemaElementSchema
            {
                Name = "TestAction",
                TargetUri = "test://uri",
                TimeOutInSeconds = 60,
                Topic = "action/topic",
                TypeRef = "TestTypeRef",
                ActionType = AdrBaseService.AssetManagementGroupActionType.Read,
                ActionConfiguration = "{ \"action\": \"config\" }"
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestAction", result.Name);
            Assert.Equal("test://uri", result.TargetUri);
            Assert.Equal(60, result.TimeOutInSeconds);
            Assert.Equal("action/topic", result.Topic);
            Assert.Equal("TestTypeRef", result.TypeRef);
            Assert.Equal(AssetManagementGroupActionType.Read, result.ActionType);
            Assert.NotNull(result.ActionConfiguration);
        }

        [Fact]
        public void DatasetDestination_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.DatasetDestination
            {
                Target = AdrBaseService.DatasetTarget.Mqtt,
                Configuration = new AdrBaseService.DestinationConfiguration
                {
                    Key = "test-key",
                    Path = "test/path",
                    Topic = "test/topic",
                    Qos = AdrBaseService.Qos.AtLeastOnce,
                    Retain = AdrBaseService.Retain.True,
                    Ttl = 300
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(DatasetTarget.Mqtt, result.Target);
            Assert.NotNull(result.Configuration);
            Assert.Equal("test-key", result.Configuration.Key);
            Assert.Equal("test/path", result.Configuration.Path);
            Assert.Equal("test/topic", result.Configuration.Topic);
            Assert.Equal(QoS.AtLeastOnce, result.Configuration.Qos);
            Assert.Equal(Retain.True, result.Configuration.Retain);
            Assert.Equal(300, result.Configuration.Ttl);
        }

        [Fact]
        public void EventStreamDestination_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.EventStreamDestination
            {
                Target = AdrBaseService.EventStreamTarget.Kafka,
                Configuration = new AdrBaseService.DestinationConfiguration
                {
                    Key = "event-key",
                    Path = "event/path"
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(EventStreamTarget.Kafka, result.Target);
            Assert.NotNull(result.Configuration);
            Assert.Equal("event-key", result.Configuration.Key);
            Assert.Equal("event/path", result.Configuration.Path);
        }

        [Fact]
        public void Authentication_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.AuthenticationSchema
            {
                Method = AdrBaseService.MethodSchema.Certificate,
                X509credentials = new AdrBaseService.X509credentialsSchema
                {
                    CertificateSecretName = "test-cert-secret"
                },
                UsernamePasswordCredentials = new AdrBaseService.UsernamePasswordCredentialsSchema
                {
                    UsernameSecretName = "test-username-secret",
                    PasswordSecretName = "test-password-secret"
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(Method.Certificate, result.Method);
            Assert.NotNull(result.X509Credentials);
            Assert.Equal("test-cert-secret", result.X509Credentials.CertificateSecretName);
            Assert.NotNull(result.UsernamePasswordCredentials);
            Assert.Equal("test-username-secret", result.UsernamePasswordCredentials.UsernameSecretName);
            Assert.Equal("test-password-secret", result.UsernamePasswordCredentials.PasswordSecretName);
        }

        [Fact]
        public void DeviceSpecification_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.DeviceSpecificationSchema
            {
                Enabled = true,
                Manufacturer = "Device Manufacturer",
                Model = "Device Model",
                Uuid = "device-uuid",
                Version = "1.0",
                DiscoveredDeviceRef = "discovered-device-ref",
                ExternalDeviceId = "external-device-id",
                LastTransitionTime = "2023-01-01T00:00:00Z",
                OperatingSystemVersion = "OS v1.0",
                Attributes = new Dictionary<string, string> { { "attr1", "value1" } },
                Endpoints = new AdrBaseService.DeviceEndpointsSchema
                {
                    Inbound = new Dictionary<string, AdrBaseService.InboundSchemaMapValueSchema>
                    {
                        {
                            "endpoint1", new AdrBaseService.InboundSchemaMapValueSchema
                            {
                                Address = "test://address",
                                EndpointType = "TestType",
                                Version = "1.0"
                            }
                        }
                    }
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Enabled);
            Assert.Equal("Device Manufacturer", result.Manufacturer);
            Assert.Equal("Device Model", result.Model);
            Assert.Equal("device-uuid", result.Uuid);
            Assert.Equal("1.0", result.Version);
            Assert.Equal("discovered-device-ref", result.DiscoveredDeviceRef);
            Assert.Equal("external-device-id", result.ExternalDeviceId);
            Assert.Equal("2023-01-01T00:00:00Z", result.LastTransitionTime);
            Assert.Equal("OS v1.0", result.OperatingSystemVersion);
            Assert.NotNull(result.Attributes);
            Assert.Equal("value1", result.Attributes["attr1"]);
            Assert.NotNull(result.Endpoints);
            Assert.NotNull(result.Endpoints.Inbound);
            Assert.Contains("endpoint1", result.Endpoints.Inbound.Keys);
        }

        [Fact]
        public void DeviceStatus_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.DeviceStatus
            {
                Config = new AdrBaseService.DeviceStatusConfigSchema
                {
                    Version = 3,
                    LastTransitionTime = "2023-01-01T00:00:00Z"
                },
                Endpoints = new AdrBaseService.DeviceStatusEndpointSchema
                {
                    Inbound = new Dictionary<string, AdrBaseService.DeviceStatusInboundEndpointSchemaMapValueSchema>
                    {
                        {
                            "status-endpoint", new AdrBaseService.DeviceStatusInboundEndpointSchemaMapValueSchema
                            {
                                Error = new AdrBaseService.ConfigError
                                {
                                    Code = "ERROR_001",
                                    Message = "Test error message"
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
            Assert.NotNull(result.Config);
            Assert.Equal(3, result.Config.Version);
            Assert.Equal("2023-01-01T00:00:00Z", result.Config.LastTransitionTime);
            Assert.NotNull(result.Endpoints);
            Assert.NotNull(result.Endpoints.Inbound);
            Assert.Contains("status-endpoint", result.Endpoints.Inbound.Keys);
            Assert.NotNull(result.Endpoints.Inbound["status-endpoint"].Error);
            Assert.Equal("ERROR_001", result.Endpoints.Inbound["status-endpoint"].Error.Code);
            Assert.Equal("Test error message", result.Endpoints.Inbound["status-endpoint"].Error.Message);
        }

        [Fact]
        public void ConfigError_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.ConfigError
            {
                Code = "CONFIG_ERROR",
                Message = "Configuration error occurred",
                InnerError = "Inner error details",
                Details = new List<AdrBaseService.DetailsSchemaElementSchema>
                {
                    new AdrBaseService.DetailsSchemaElementSchema
                    {
                        Code = "DETAIL_001",
                        Message = "Detail message",
                        Info = "Additional info",
                        CorrelationId = "correlation-123"
                    }
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("CONFIG_ERROR", result.Code);
            Assert.Equal("Configuration error occurred", result.Message);
            Assert.Equal("Inner error details", result.InnerError);
            Assert.NotNull(result.Details);
            Assert.Single(result.Details);
            Assert.Equal("DETAIL_001", result.Details[0].Code);
            Assert.Equal("Detail message", result.Details[0].Message);
            Assert.Equal("Additional info", result.Details[0].Info);
            Assert.Equal("correlation-123", result.Details[0].CorrelationId);
        }

        [Fact]
        public void AssetConfigStatus_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.AssetConfigStatusSchema
            {
                Version = 5,
                LastTransitionTime = "2023-01-01T00:00:00Z",
                Error = new AdrBaseService.ConfigError
                {
                    Code = "ASSET_ERROR",
                    Message = "Asset configuration error"
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.Version);
            Assert.Equal("2023-01-01T00:00:00Z", result.LastTransitionTime);
            Assert.NotNull(result.Error);
            Assert.Equal("ASSET_ERROR", result.Error.Code);
            Assert.Equal("Asset configuration error", result.Error.Message);
        }

        [Fact]
        public void AssetDatasetEventStreamStatus_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.AssetDatasetEventStreamStatus
            {
                Name = "TestEventStream",
                MessageSchemaReference = new AdrBaseService.MessageSchemaReference
                {
                    SchemaName = "EventStreamSchema",
                    SchemaRegistryNamespace = "EventNamespace",
                    SchemaVersion = "2.0"
                },
                Error = new AdrBaseService.ConfigError
                {
                    Code = "STREAM_ERROR",
                    Message = "Event stream error"
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestEventStream", result.Name);
            Assert.NotNull(result.MessageSchemaReference);
            Assert.Equal("EventStreamSchema", result.MessageSchemaReference.SchemaName);
            Assert.Equal("EventNamespace", result.MessageSchemaReference.SchemaRegistryNamespace);
            Assert.Equal("2.0", result.MessageSchemaReference.SchemaVersion);
            Assert.NotNull(result.Error);
            Assert.Equal("STREAM_ERROR", result.Error.Code);
            Assert.Equal("Event stream error", result.Error.Message);
        }

        [Fact]
        public void AssetDeviceRef_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.AssetDeviceRef
            {
                DeviceName = "RefDevice",
                EndpointName = "RefEndpoint"
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("RefDevice", result.DeviceName);
            Assert.Equal("RefEndpoint", result.EndpointName);
        }

        [Fact]
        public void DeviceOutboundEndpoint_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.DeviceOutboundEndpoint
            {
                Address = "outbound://address",
                EndpointType = "OutboundType"
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("outbound://address", result.Address);
            Assert.Equal("OutboundType", result.EndpointType);
        }

        [Fact]
        public void OutboundSchema_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.OutboundSchema
            {
                Assigned = new Dictionary<string, AdrBaseService.DeviceOutboundEndpoint>
                {
                    {
                        "assigned1", new AdrBaseService.DeviceOutboundEndpoint
                        {
                            Address = "assigned://address",
                            EndpointType = "AssignedType"
                        }
                    }
                },
                Unassigned = new Dictionary<string, AdrBaseService.DeviceOutboundEndpoint>
                {
                    {
                        "unassigned1", new AdrBaseService.DeviceOutboundEndpoint
                        {
                            Address = "unassigned://address",
                            EndpointType = "UnassignedType"
                        }
                    }
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Assigned);
            Assert.NotNull(result.Unassigned);
            Assert.Contains("assigned1", result.Assigned.Keys);
            Assert.Contains("unassigned1", result.Unassigned.Keys);
            Assert.Equal("assigned://address", result.Assigned["assigned1"].Address);
            Assert.Equal("unassigned://address", result.Unassigned["unassigned1"].Address);
        }

        [Fact]
        public void CreateDiscoveredAssetEndpointProfileResponse_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange
            var source = new AdrBaseService.DiscoveredDeviceResponseSchema
            {
                DiscoveryId = "endpoint-profile-discovery-id",
                Version = "2.0"
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("endpoint-profile-discovery-id", result.DiscoveryId);
            Assert.Equal("2.0", result.Version);
        }

        [Fact]
        public void EnumConversions_ToModel_ShouldConvertSuccessfully()
        {
            // Test Retain enum conversion
            Assert.Equal(Retain.True, AdrBaseService.Retain.True.ToModel());
            Assert.Equal(Retain.False, AdrBaseService.Retain.False.ToModel());

            // Test QoS enum conversion
            Assert.Equal(QoS.AtMostOnce, AdrBaseService.Qos.AtMostOnce.ToModel());
            Assert.Equal(QoS.AtLeastOnce, AdrBaseService.Qos.AtLeastOnce.ToModel());
            Assert.Equal(QoS.ExactlyOnce, AdrBaseService.Qos.ExactlyOnce.ToModel());

            // Test DatasetTarget enum conversion
            Assert.Equal(DatasetTarget.Mqtt, AdrBaseService.DatasetTarget.Mqtt.ToModel());

            // Test EventStreamTarget enum conversion
            Assert.Equal(EventStreamTarget.Kafka, AdrBaseService.EventStreamTarget.Kafka.ToModel());

            // Test Method enum conversion
            Assert.Equal(Method.Anonymous, AdrBaseService.MethodSchema.Anonymous.ToModel());
            Assert.Equal(Method.Certificate, AdrBaseService.MethodSchema.Certificate.ToModel());
            Assert.Equal(Method.UsernamePassword, AdrBaseService.MethodSchema.UsernamePassword.ToModel());

            // Test AssetManagementGroupActionType enum conversion
            Assert.Equal(AssetManagementGroupActionType.Read, AdrBaseService.AssetManagementGroupActionType.Read.ToModel());
            Assert.Equal(AssetManagementGroupActionType.Write, AdrBaseService.AssetManagementGroupActionType.Write.ToModel());
        }

        [Fact]
        public void JsonDocumentParsing_ToModel_ShouldHandleValidJson()
        {
            // Arrange
            var source = new AdrBaseService.AssetSpecificationSchema
            {
                DisplayName = "JSON Test Asset",
                DeviceRef = new AdrBaseService.AssetDeviceRef
                {
                    DeviceName = "TestDevice",
                    EndpointName = "TestEndpoint"
                },
                DefaultDatasetsConfiguration = "{ \"datasetKey\": \"datasetValue\", \"number\": 42 }",
                DefaultEventsConfiguration = "{ \"eventKey\": \"eventValue\", \"enabled\": true }",
                DefaultManagementGroupsConfiguration = "{ \"mgmtKey\": \"mgmtValue\" }",
                DefaultStreamsConfiguration = "{ \"streamKey\": \"streamValue\" }"
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.DefaultDatasetsConfiguration);
            Assert.NotNull(result.DefaultEventsConfiguration);
            Assert.NotNull(result.DefaultManagementGroupsConfiguration);
            Assert.NotNull(result.DefaultStreamsConfiguration);

            // Verify JSON content can be accessed
            Assert.True(result.DefaultDatasetsConfiguration.RootElement.TryGetProperty("datasetKey", out var datasetProp));
            Assert.Equal("datasetValue", datasetProp.GetString());
            
            Assert.True(result.DefaultEventsConfiguration.RootElement.TryGetProperty("enabled", out var eventProp));
            Assert.True(eventProp.GetBoolean());
        }

        [Fact]
        public void ComplexNestedStructures_ToModel_ShouldConvertSuccessfully()
        {
            // Arrange - Test a complex nested structure with multiple levels
            var source = new AdrBaseService.AssetSpecificationSchema
            {
                DisplayName = "Complex Asset",
                DeviceRef = new AdrBaseService.AssetDeviceRef
                {
                    DeviceName = "ComplexDevice",
                    EndpointName = "ComplexEndpoint"
                },
                Datasets = new List<AdrBaseService.AssetDatasetSchemaElementSchema>
                {
                    new AdrBaseService.AssetDatasetSchemaElementSchema
                    {
                        Name = "ComplexDataset",
                        DataPoints = new List<AdrBaseService.AssetDatasetDataPointSchemaElementSchema>
                        {
                            new AdrBaseService.AssetDatasetDataPointSchemaElementSchema
                            {
                                Name = "DataPoint1",
                                DataSource = "Source1",
                                DataPointConfiguration = "{ \"config1\": \"value1\" }"
                            },
                            new AdrBaseService.AssetDatasetDataPointSchemaElementSchema
                            {
                                Name = "DataPoint2",
                                DataSource = "Source2"
                            }
                        },
                        Destinations = new List<AdrBaseService.DatasetDestination>
                        {
                            new AdrBaseService.DatasetDestination
                            {
                                Target = AdrBaseService.DatasetTarget.Mqtt,
                                Configuration = new AdrBaseService.DestinationConfiguration
                                {
                                    Topic = "complex/topic",
                                    Qos = AdrBaseService.Qos.AtLeastOnce
                                }
                            }
                        }
                    }
                },
                Events = new List<AdrBaseService.AssetEventSchemaElementSchema>
                {
                    new AdrBaseService.AssetEventSchemaElementSchema
                    {
                        Name = "ComplexEvent",
                        EventConfiguration = "{ \"eventConfig\": \"eventValue\" }",
                        DataPoints = new List<AdrBaseService.AssetEventDataPointSchemaElementSchema>
                        {
                            new AdrBaseService.AssetEventDataPointSchemaElementSchema
                            {
                                Name = "EventDataPoint",
                                DataSource = "EventSource",
                                DataPointConfiguration = "{ \"eventDataConfig\": \"eventDataValue\" }"
                            }
                        }
                    }
                }
            };

            // Act
            var result = source.ToModel();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Complex Asset", result.DisplayName);
            Assert.NotNull(result.Datasets);
            Assert.Single(result.Datasets);
            Assert.Equal("ComplexDataset", result.Datasets[0].Name);
            Assert.NotNull(result.Datasets[0].DataPoints);
            Assert.Equal(2, result.Datasets[0].DataPoints.Count);
            Assert.Equal("DataPoint1", result.Datasets[0].DataPoints[0].Name);
            Assert.NotNull(result.Datasets[0].DataPoints[0].DataPointConfiguration);
            Assert.NotNull(result.Datasets[0].Destinations);
            Assert.Single(result.Datasets[0].Destinations);
            Assert.Equal(DatasetTarget.Mqtt, result.Datasets[0].Destinations[0].Target);

            Assert.NotNull(result.Events);
            Assert.Single(result.Events);
            Assert.Equal("ComplexEvent", result.Events[0].Name);
            Assert.NotNull(result.Events[0].EventConfiguration);
            Assert.NotNull(result.Events[0].DataPoints);
            Assert.Single(result.Events[0].DataPoints);
            Assert.Equal("EventDataPoint", result.Events[0].DataPoints[0].Name);
            Assert.NotNull(result.Events[0].DataPoints[0].DataPointConfiguration);
        }
    }
}
