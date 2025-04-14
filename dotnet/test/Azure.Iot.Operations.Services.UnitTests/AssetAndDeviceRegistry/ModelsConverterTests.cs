using AdrBaseService = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AepTypeService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.AssetAndDeviceRegistry;

public class ModelsConverterTests
{
    [Fact]
    public void ToModel_AssetStatus_ConvertsCorrectly()
    {
        var source = new AdrBaseService.AssetStatus
        {
            Errors = new List<AdrBaseService.Error>
            {
                new() { Code = 1, Message = "Message1" }
            }
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Single(result.Errors!);
        Assert.Equal(1, result.Errors?[0].Code);
        Assert.Equal("Message1", result.Errors?[0].Message);
    }

    [Fact]
    public void ToModel_DatasetsSchemaSchemaElementSchema_ConvertsCorrectly()
    {
        var source = new AdrBaseService.DatasetsSchemaSchemaElementSchema
        {
            Name = "TestName",
            MessageSchemaReference = new AdrBaseService.MessageSchemaReference
            {
                SchemaName = "TestSchema",
                SchemaNamespace = "TestNamespace",
                SchemaVersion = "1.0"
            }
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal("TestName", result.Name);
        Assert.NotNull(result.MessageSchemaReference);
        Assert.Equal("TestSchema", result.MessageSchemaReference.SchemaName);
        Assert.Equal("TestNamespace", result.MessageSchemaReference.SchemaNamespace);
        Assert.Equal("1.0", result.MessageSchemaReference.SchemaVersion);
    }

    [Fact]
    public void ToModel_EventsSchemaSchemaElementSchema_ConvertsCorrectly()
    {
        var source = new AdrBaseService.EventsSchemaSchemaElementSchema
        {
            Name = "TestName",
            MessageSchemaReference = new AdrBaseService.MessageSchemaReference
            {
                SchemaName = "TestSchema",
                SchemaNamespace = "TestNamespace",
                SchemaVersion = "1.0"
            }
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal("TestName", result.Name);
        Assert.NotNull(result.MessageSchemaReference);
        Assert.Equal("TestSchema", result.MessageSchemaReference.SchemaName);
        Assert.Equal("TestNamespace", result.MessageSchemaReference.SchemaNamespace);
        Assert.Equal("1.0", result.MessageSchemaReference.SchemaVersion);
    }

    [Fact]
    public void ToModel_Asset_ConvertsCorrectly()
    {
        var source = new AdrBaseService.Asset
        {
            Name = "TestAsset",
            Specification = new AdrBaseService.AssetSpecificationSchema
            {
                Description = "Test Description",
                Enabled = true,
                Manufacturer = "Test Manufacturer",
                Model = "Test Model",
                Uuid = "12345",
                Version = "1.0",
                DocumentationUri = "http://example.com",
                HardwareRevision = "1.0",
                ManufacturerUri = "http://example.com",
                ProductCode = "Test ProductCode",
                DefaultDatasetsConfiguration = "Test DefaultDatasetsConfiguration",
                DefaultEventsConfiguration = "Test DefaultEventsConfiguration",
                DefaultTopic = new AdrBaseService.Topic
                {
                    Path = "TestPath",
                    Retain = AdrBaseService.RetainSchema.Keep
                },
                SerialNumber = "Test SerialNumber",
                SoftwareRevision = "Test SoftwareRevision",
                AssetEndpointProfileRef = "Test AssetEndpointProfileRef",
                DisplayName = "Test DisplayName",
                ExternalAssetId = "Test ExternalAssetId",
                DiscoveredAssetRefs = new List<string> { "Test DiscoveredAssetRef1", "Test DiscoveredAssetRef2" },
                Events =
                [
                    new()
                    {
                        Name = "TestEvent",
                        Topic = new AdrBaseService.Topic
                        {
                            Path = "TestPath",
                            Retain = AdrBaseService.RetainSchema.Keep
                        },
                        EventConfiguration = "TestConfig",
                        ObservabilityMode = AdrBaseService.AssetEventObservabilityModeSchema.Log,
                        EventNotifier = "TestNotifier",
                    }
                ],
                Datasets =
                [
                    new()
                    {
                        Name = "TestName",
                        DataPoints = new List<AdrBaseService.AssetDataPointSchemaElementSchema>
                        {
                            new()
                            {
                                DataPointConfiguration = "TestConfig",
                                DataSource = "TestSource",
                                Name = "TestName",
                                ObservabilityMode = AdrBaseService.AssetDataPointObservabilityModeSchema.Counter
                            }
                        },
                        Topic = new AdrBaseService.Topic
                        {
                            Path = "TestPath",
                            Retain = AdrBaseService.RetainSchema.Keep
                        },
                        DatasetConfiguration = "TestDatasetConfiguration"
                    }
                ],
                Attributes = new Dictionary<string, string>{ {"TestKey", "TestValue" } },
            },
            Status = new AdrBaseService.AssetStatus
            {
                Errors = [new() { Code = 1, Message = "Message1" }]
            }
        };
        var result = source.ToModel();
        Assert.NotNull(result);
        Assert.Equal("TestAsset", result.Name);
        Assert.NotNull(result.Specification);
        Assert.Equal("Test Description", result.Specification.Description);
        Assert.True(result.Specification.Enabled);
        Assert.Equal("Test Manufacturer", result.Specification.Manufacturer);
        Assert.Equal("Test Model", result.Specification.Model);
        Assert.Equal("12345", result.Specification.Uuid);
        Assert.Equal("1.0", result.Specification.Version);
        Assert.NotNull(result.Status);
        Assert.Single(result.Status.Errors!);
        Assert.Equal(1, result.Status.Errors?[0].Code);
        Assert.Equal("Message1", result.Status.Errors?[0].Message);
    }

    [Fact]
    public void ToModel_MessageSchemaReference_ConvertsCorrectly()
    {
        var source = new AdrBaseService.MessageSchemaReference
        {
            SchemaName = "TestSchema",
            SchemaNamespace = "TestNamespace",
            SchemaVersion = "1.0"
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal("TestSchema", result.SchemaName);
        Assert.Equal("TestNamespace", result.SchemaNamespace);
        Assert.Equal("1.0", result.SchemaVersion);
    }

    [Fact]
    public void ToModel_AssetSpecification_ConvertsCorrectly()
    {
        var source = new AdrBaseService.AssetSpecificationSchema
        {
            Description = "Test Asset",
            Enabled = true,
            Manufacturer = "Test Manufacturer",
            Model = "Test Model",
            Uuid = "12345",
            Version = "1.0"
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal("Test Asset", result.Description);
        Assert.True(result.Enabled);
        Assert.Equal("Test Manufacturer", result.Manufacturer);
        Assert.Equal("Test Model", result.Model);
        Assert.Equal("12345", result.Uuid);
        Assert.Equal("1.0", result.Version);
    }

    [Fact]
    public void ToModel_AssetDatasetSchemaElementSchema_ConvertsCorrectly()
    {
        var source = new AdrBaseService.AssetDatasetSchemaElementSchema
        {
            Name = "TestName",
            DataPoints = [new() { Name = "TestDataPoint", ObservabilityMode = AdrBaseService.AssetDataPointObservabilityModeSchema.Counter }],
            DatasetConfiguration = "TestConfig",
            Topic = new AdrBaseService.Topic(){
                Path = "TestPath",
                Retain = AdrBaseService.RetainSchema.Keep
            },
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal("TestName", result.Name);
        Assert.NotNull(result.DataPoints);
        Assert.Single(result.DataPoints);
        Assert.Equal("TestDataPoint", result.DataPoints?[0].Name);
        Assert.Equal(AssetDataPointObservabilityMode.Counter, result.DataPoints?[0].ObservabilityMode);
        Assert.Equal("TestConfig", result.DatasetConfiguration);
        Assert.NotNull(result.Topic);
        Assert.Equal("TestPath", result.Topic.Path);
        Assert.Equal(Retain.Keep, result.Topic.Retain);
    }

    [Fact]
    public void ToModel_AssetEventSchemaElementSchema_ConvertsCorrectly()
    {
        var source = new AdrBaseService.AssetEventSchemaElementSchema
        {
            Name = "TestName",
            Topic = new AdrBaseService.Topic
            {
                Path = "TestPath",
                Retain = AdrBaseService.RetainSchema.Keep
            },
            EventConfiguration = "TestConfig",
            ObservabilityMode = AdrBaseService.AssetEventObservabilityModeSchema.Log,
            EventNotifier = "TestNotifier",
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal("TestName", result.Name);
        Assert.NotNull(result.Topic);
        Assert.Equal("TestPath", result.Topic.Path);
        Assert.Equal(Retain.Keep, result.Topic.Retain);
        Assert.Equal("TestConfig", result.EventConfiguration);
        Assert.Equal(AssetEventObservabilityMode.Log, result.ObservabilityMode);
        Assert.Equal("TestNotifier", result.EventNotifier);
    }

    [Fact]
    public void ToModel_AssetEventObservabilityModeSchema_ConvertsCorrectly()
    {
        var source = AdrBaseService.AssetEventObservabilityModeSchema.Log;

        var result = source.ToModel();

        Assert.Equal(AssetEventObservabilityMode.Log, result);

        source = AdrBaseService.AssetEventObservabilityModeSchema.None;

        result = source.ToModel();

        Assert.Equal(AssetEventObservabilityMode.None, result);
    }

    [Fact]
    public void ToModel_Topic_ConvertsCorrectly()
    {
        var source = new AdrBaseService.Topic
        {
            Path = "TestPath",
            Retain = AdrBaseService.RetainSchema.Keep
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal("TestPath", result.Path);
        Assert.Equal(Retain.Keep, result.Retain);
    }

    [Fact]
    public void ToModel_RetainSchema_ConvertsCorrectly()
    {
        var source = AdrBaseService.RetainSchema.Keep;

        var result = source.ToModel();

        Assert.Equal(Retain.Keep, result);

        source = AdrBaseService.RetainSchema.Never;

        result = source.ToModel();

        Assert.Equal(Retain.Never, result);
    }

    [Fact]
    public void ToModel_AssetDataPointSchemaElementSchema_ConvertsCorrectly()
    {
        var source = new AdrBaseService.AssetDataPointSchemaElementSchema
        {
            DataPointConfiguration = "TestConfig",
            DataSource = "TestSource",
            Name = "TestName",
            ObservabilityMode = AdrBaseService.AssetDataPointObservabilityModeSchema.Counter
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal("TestConfig", result.DataPointConfiguration);
        Assert.Equal("TestSource", result.DataSource);
        Assert.Equal("TestName", result.Name);
        Assert.Equal(AssetDataPointObservabilityMode.Counter, result.ObservabilityMode);
    }

    [Fact]
    public void ToModel_AssetDataPointObservabilityModeSchema_ConvertsCorrectly()
    {
        var source = AdrBaseService.AssetDataPointObservabilityModeSchema.Counter;
        var result = source.ToModel();
        Assert.Equal(AssetDataPointObservabilityMode.Counter, result);

        source = AdrBaseService.AssetDataPointObservabilityModeSchema.Gauge;
        result = source.ToModel();
        Assert.Equal(AssetDataPointObservabilityMode.Gauge, result);

        source = AdrBaseService.AssetDataPointObservabilityModeSchema.None;
        result = source.ToModel();
        Assert.Equal(AssetDataPointObservabilityMode.None, result);

        source = AdrBaseService.AssetDataPointObservabilityModeSchema.Log;
        result = source.ToModel();
        Assert.Equal(AssetDataPointObservabilityMode.Log, result);

        source = AdrBaseService.AssetDataPointObservabilityModeSchema.Histogram;
        result = source.ToModel();
        Assert.Equal(AssetDataPointObservabilityMode.Histogram, result);

    }

    [Fact]
    public void ToModel_DetectedAssetDataPointSchemaElementSchema_ConvertsCorrectly()
    {
        var source = new AdrBaseService.DetectedAssetDataPointSchemaElementSchema
        {
            DataPointConfiguration = "TestConfig",
            DataSource = "TestSource",
            Name = "TestName",
            LastUpdatedOn = "2023-10-01T00:00:00Z",
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal("TestConfig", result.DataPointConfiguration);
        Assert.Equal("TestSource", result.DataSource);
        Assert.Equal("TestName", result.Name);
        Assert.NotEqual("2023-10-01T00:00:00Z", result.LastUpdatedOn);
    }

    [Fact]
    public void ToModel_NotificationResponse_ConvertsCorrectly()
    {
        var source = AdrBaseService.NotificationResponse.Accepted;
        var result = source.ToModel();
        Assert.Equal(NotificationResponse.Accepted, result);

        source = AdrBaseService.NotificationResponse.Failed;
        result = source.ToModel();
        Assert.Equal(NotificationResponse.Failed, result);
    }

    [Fact]
    public void ToModel_AssetEndpointProfile_ConvertCorrectly()
    {
        var source = new AdrBaseService.AssetEndpointProfile
        {
            Name = "TestName",
            Specification = new AdrBaseService.AssetEndpointProfileSpecificationSchema
            {
                Uuid = "TestUuid",
                TargetAddress = "TestAddress",
                EndpointProfileType = "TestType",
                AdditionalConfiguration = "TestConfig",
                Authentication = new AdrBaseService.AuthenticationSchema
                {
                    Method = AdrBaseService.MethodSchema.Certificate
                }
            },
            Status = new AdrBaseService.AssetEndpointProfileStatus()
            {
                Errors = new List<AdrBaseService.Error>
                {
                    new() { Code = 1, Message = "Message1" }
                }
            }
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal("TestName", result.Name);
        Assert.NotNull(result.Specification);
        Assert.Equal("TestUuid", result.Specification.Uuid);
        Assert.Equal("TestAddress", result.Specification.TargetAddress);
        Assert.Equal("TestType", result.Specification.EndpointProfileType);
        Assert.NotNull(result.Status);
        Assert.Single(result.Status.Errors!);
        Assert.Equal(1, result.Status.Errors?[0].Code);
        Assert.Equal("Message1", result.Status.Errors?[0].Message);
    }

    [Fact]
    public void ToModel_UsernamePasswordCredentialsSchema_ConvertsCorrectly()
    {
        var source = new AdrBaseService.UsernamePasswordCredentialsSchema
        {
            UsernameSecretName = "TestUser",
            PasswordSecretName = "TestPassword"
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal("TestUser", result.UsernameSecretName);
        Assert.Equal("TestPassword", result.PasswordSecretName);
    }

    [Fact]
    public void ToModel_AssetEndpointProfileSpecification_ConvertsCorrectly()
    {
        var source = new AdrBaseService.AssetEndpointProfileSpecificationSchema
        {
            Uuid = "endpoint-uuid",
            TargetAddress = "192.168.1.1",
            EndpointProfileType = "TestType"
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal("endpoint-uuid", result.Uuid);
        Assert.Equal("192.168.1.1", result.TargetAddress);
        Assert.Equal("TestType", result.EndpointProfileType);
    }

    [Fact]
    public void ToModel_Authentication_ConvertsCorrectly()
    {
        var source = new AdrBaseService.AuthenticationSchema
        {
            Method = AdrBaseService.MethodSchema.Certificate,
            X509credentials = new AdrBaseService.X509credentialsSchema { CertificateSecretName = "cert-secret" }
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal(Method.Certificate, result.Method);
        Assert.NotNull(result.X509Credentials);
        Assert.Equal("cert-secret", result.X509Credentials.CertificateSecretName);
    }

    [Fact]
    public void ToModel_CreateDetectedAssetResponse_ConvertsCorrectly()
    {
        var source = new AdrBaseService.CreateDetectedAssetResponseSchema
        {
            Status = AdrBaseService.DetectedAssetResponseStatusSchema.Created
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal(DetectedAssetResponseStatus.Created, result.Status);
    }

    [Fact]
    public void ToModel_CreateDiscoveredAssetEndpointProfileResponse_ConvertsCorrectly()
    {
        var source = new CreateDiscoveredAssetEndpointProfileResponseSchema
        {
            Status = DiscoveredAssetEndpointProfileResponseStatusSchema.Created
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal(DiscoveredAssetEndpointProfileResponseStatus.Created, result.Status);
    }

    [Fact]
    public void ToModel_AssetEndpointProfileStatus_ConvertsCorrectly()
    {
        var source = new AdrBaseService.AssetEndpointProfileStatus
        {
            Errors = new List<AdrBaseService.Error>
            {
                new() { Code = 2, Message = "Error message" }
            }
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Single(result.Errors!);
        Assert.Equal(2, result.Errors?[0].Code);
        Assert.Equal("Error message", result.Errors?[0].Message);
    }

    [Fact]
    public void ToModel_Error_ConvertsCorrectly()
    {
        var source = new AdrBaseService.Error
        {
            Code = 404,
            Message = "Not Found"
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal(404, result.Code);
        Assert.Equal("Not Found", result.Message);
    }

    [Fact]
    public void ToModel_MethodSchema_ConvertsCorrectly()
    {
        var source = AdrBaseService.MethodSchema.Certificate;

        var result = source.ToModel();

        Assert.Equal(Method.Certificate, result);

        source = AdrBaseService.MethodSchema.UsernamePassword;

        result = source.ToModel();

        Assert.Equal(Method.UsernamePassword, result);
    }

    [Fact]
    public void ToModel_X509CredentialsSchema_ConvertsCorrectly()
    {
        var source = new AdrBaseService.X509credentialsSchema
        {
            CertificateSecretName = "TestCertSecret"
        };

        var result = source.ToModel();

        Assert.NotNull(result);
        Assert.Equal("TestCertSecret", result.CertificateSecretName);
    }
}
