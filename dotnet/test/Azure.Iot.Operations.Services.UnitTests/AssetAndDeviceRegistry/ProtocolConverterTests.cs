using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AepTypeService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Xunit;
using AdrBaseService = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;

namespace Azure.Iot.Operations.Services.UnitTests.AssetAndDeviceRegistry;

public class ProtocolConverterTests
{
    [Fact]
    public void ToProtocol_GetAssetRequest_ConvertsCorrectly()
    {
        var source = new GetAssetRequest { AssetName = "TestAsset" };
        var result = source.ToProtocol();
        Assert.NotNull(result);
        Assert.Equal("TestAsset", result.AssetName);
    }

    [Fact]
    public void ToProtocol_UpdateAssetStatusRequest_ConvertsCorrectly()
    {
        var source = new UpdateAssetStatusRequest
        {
            AssetName = "TestAsset",
            AssetStatus = new AssetStatus { Version = 1 }
        };
        var result = source.ToProtocol();
        Assert.NotNull(result);
        Assert.Equal("TestAsset", result.AssetStatusUpdate.AssetName);
        Assert.Equal(1, result.AssetStatusUpdate.AssetStatus?.Version);
    }

    [Fact]
    public void ToProtocol_AssetStatus_ConvertsCorrectly()
    {
        var source = new AssetStatus
        {
            Version = 1,
            Errors = [new Error { Code = 1, Message = "Error1" }],
            DatasetsSchema =
            [
                new DatasetsSchemaElement
                {
                    Name = "Dataset1",
                    MessageSchemaReference = new MessageSchemaReference
                    {
                        SchemaName = "TestSchemaName",
                        SchemaNamespace = "TestSchemaNamespace",
                        SchemaVersion = "1.0"
                    }
                }
            ],
            EventsSchema = [new EventsSchemaElement
            {
                Name = "Event1",
                MessageSchemaReference = new MessageSchemaReference
                {
                    SchemaName = "TestEventSchemaName",
                    SchemaNamespace = "TestEventSchemaNamespace",
                    SchemaVersion = "1.0",
                }
            }]
        };

        var result = source.ToProtocol();

        Assert.NotNull(result);
        Assert.Equal(1, result.Version);
        Assert.NotNull(result.Errors);
        Assert.Single(result.Errors);
        Assert.Equal(1, result.Errors[0].Code);
        Assert.Equal("Error1", result.Errors[0].Message);
        Assert.NotNull(result.DatasetsSchema);
        Assert.Single(result.DatasetsSchema);
        Assert.Equal("Dataset1", result.DatasetsSchema[0].Name);
        Assert.NotNull(result.DatasetsSchema[0].MessageSchemaReference);
        Assert.Equal("TestSchemaName", result.DatasetsSchema[0].MessageSchemaReference?.SchemaName);
        Assert.Equal("TestSchemaNamespace", result.DatasetsSchema[0].MessageSchemaReference?.SchemaNamespace);
        Assert.Equal("1.0", result.DatasetsSchema[0].MessageSchemaReference?.SchemaVersion);
        Assert.NotNull(result.EventsSchema);
        Assert.Single(result.EventsSchema);
        Assert.Equal("Event1", result.EventsSchema[0].Name);
        Assert.NotNull(result.EventsSchema[0].MessageSchemaReference);
        Assert.Equal("TestEventSchemaName", result.EventsSchema[0].MessageSchemaReference?.SchemaName);
        Assert.Equal("TestEventSchemaNamespace", result.EventsSchema[0].MessageSchemaReference?.SchemaNamespace);
        Assert.Equal("1.0", result.EventsSchema[0].MessageSchemaReference?.SchemaVersion);
    }

    [Fact]
    public void ToProtocol_Error_ConvertsCorrectly()
    {
        var source = new Error { Code = 404, Message = "Not Found" };
        var result = source.ToProtocol();
        Assert.NotNull(result);
        Assert.Equal(404, result.Code);
        Assert.Equal("Not Found", result.Message);
    }

    [Fact]
    public void ToProtocol_MessageSchemaReference_ConvertsCorrectly()
    {
        var source = new MessageSchemaReference
        {
            SchemaName = "TestSchema",
            SchemaNamespace = "TestNamespace",
            SchemaVersion = "1.0"
        };
        var result = source.ToProtocol();
        Assert.NotNull(result);
        Assert.Equal("TestSchema", result.SchemaName);
        Assert.Equal("TestNamespace", result.SchemaNamespace);
        Assert.Equal("1.0", result.SchemaVersion);
    }

    [Fact]
    public void ToProtocol_Topic_ConvertsCorrectly()
    {
        var source = new Topic { Path = "TestPath", Retain = Retain.Keep };
        var result = source.ToProtocol();
        Assert.NotNull(result);
        Assert.Equal("TestPath", result.Path);
        Assert.Equal(AdrBaseService.RetainSchema.Keep, result.Retain);
    }

    [Fact]
    public void ToProtocol_Retain_ConvertsCorrectly()
    {
        var source = Retain.Keep;
        var result = source.ToProtocol();
        Assert.Equal(AdrBaseService.RetainSchema.Keep, result);
    }

    [Fact]
    public void ToProtocol_AssetDataPointObservabilityMode_ConvertsCorrectly()
    {
        var source = AssetDataPointObservabilityMode.Counter;
        var result = source.ToProtocol();
        Assert.Equal(AdrBaseService.AssetDataPointObservabilityModeSchema.Counter, result);
    }

    [Fact]
    public void ToProtocol_CreateDetectedAssetRequest_ConvertsCorrectly()
    {
        var source = new CreateDetectedAssetRequest
        {
            AssetName = "TestAsset",
            AssetEndpointProfileRef = "ProfileRef"
        };
        var result = source.ToProtocol();
        Assert.NotNull(result);
        Assert.Equal("TestAsset", result.DetectedAsset.AssetName);
        Assert.Equal("ProfileRef", result.DetectedAsset.AssetEndpointProfileRef);
    }

    [Fact]
    public void ToProtocol_CreateDiscoveredAssetEndpointProfileRequest_ConvertsCorrectly()
    {
        var source = new CreateDiscoveredAssetEndpointProfileRequest
        {
            Name = "TestProfile",
            TargetAddress = "192.168.1.1",
            EndpointProfileType = "TestType"
        };
        var result = source.ToProtocol();
        Assert.NotNull(result);
        Assert.Equal("TestProfile", result.DiscoveredAssetEndpointProfile.DaepName);
        Assert.Equal("192.168.1.1", result.DiscoveredAssetEndpointProfile.TargetAddress);
        Assert.Equal("TestType", result.DiscoveredAssetEndpointProfile.EndpointProfileType);
    }

    [Fact]
    public void ToProtocol_UpdateAssetEndpointProfileStatusRequest_ConvertsCorrectly()
    {
        var source = new UpdateAssetEndpointProfileStatusRequest
        {
            Errors = [new Error { Code = 500, Message = "Internal Error" }]
        };
        var result = source.ToProtocol();
        Assert.NotNull(result);
        Assert.NotNull(result.AssetEndpointProfileStatusUpdate);
        Assert.NotNull(result.AssetEndpointProfileStatusUpdate.Errors);
        Assert.Single(result.AssetEndpointProfileStatusUpdate.Errors);
        Assert.Equal(500, result.AssetEndpointProfileStatusUpdate.Errors[0].Code);
        Assert.Equal("Internal Error", result.AssetEndpointProfileStatusUpdate.Errors[0].Message);
    }

    [Fact]
    public void ToProtocol_DatasetsSchemaElement_ConvertsCorrectly()
    {
        var source = new DatasetsSchemaElement
        {
            Name = "Dataset1",
            MessageSchemaReference = new MessageSchemaReference { SchemaName = "Schema1" }
        };
        var result = source.ToProtocol();
        Assert.NotNull(result);
        Assert.Equal("Dataset1", result.Name);
        Assert.NotNull(result.MessageSchemaReference);
        Assert.Equal("Schema1", result.MessageSchemaReference.SchemaName);
    }

    [Fact]
    public void ToProtocol_EventsSchemaElement_ConvertsCorrectly()
    {
        var source = new EventsSchemaElement
        {
            Name = "Event1",
            MessageSchemaReference = new MessageSchemaReference { SchemaName = "SchemaEvent1" }
        };
        var result = source.ToProtocol();
        Assert.NotNull(result);
        Assert.Equal("Event1", result.Name);
        Assert.NotNull(result.MessageSchemaReference);
        Assert.Equal("SchemaEvent1", result.MessageSchemaReference.SchemaName);
    }

    [Fact]
    public void ToProtocol_DetectedAssetDatasetSchemaElement_ConvertsCorrectly()
    {
        var source = new DetectedAssetDatasetSchemaElement
        {
            Name = "DetectedDataset",
            DataSetConfiguration = "Config1",
            DataPoints = [new DetectedAssetDataPointSchemaElement { Name = "DataPoint1" }],
            Topic = new Topic { Path = "TopicPath" }
        };
        var result = source.ToProtocol();
        Assert.NotNull(result);
        Assert.Equal("DetectedDataset", result.Name);
        Assert.Equal("Config1", result.DataSetConfiguration);
        Assert.NotNull(result.DataPoints);
        Assert.Single(result.DataPoints);
        Assert.Equal("DataPoint1", result.DataPoints[0].Name);
        Assert.NotNull(result.Topic);
        Assert.Equal("TopicPath", result.Topic.Path);
    }

    [Fact]
    public void ToProtocol_DetectedAssetEventSchemaElement_ConvertsCorrectly()
    {
        var source = new DetectedAssetEventSchemaElement
        {
            Name = "DetectedEvent",
            EventConfiguration = "EventConfig",
            Topic = new Topic { Path = "EventTopicPath" }
        };
        var result = source.ToProtocol();
        Assert.NotNull(result);
        Assert.Equal("DetectedEvent", result.Name);
        Assert.Equal("EventConfig", result.EventConfiguration);
        Assert.NotNull(result.Topic);
        Assert.Equal("EventTopicPath", result.Topic.Path);
    }

    [Fact]
    public void ToProtocol_DetectedAssetDataPointSchemaElement_ConvertsCorrectly()
    {
        var source = new DetectedAssetDataPointSchemaElement
        {
            Name = "DataPoint1",
            DataPointConfiguration = "ConfigDP",
            DataSource = "Source1",
            LastUpdatedOn = "2023-10-01T00:00:00Z"
        };
        var result = source.ToProtocol();
        Assert.NotNull(result);
        Assert.Equal("DataPoint1", result.Name);
        Assert.Equal("ConfigDP", result.DataPointConfiguration);
        Assert.Equal("Source1", result.DataSource);
        Assert.Equal(source.LastUpdatedOn, result.LastUpdatedOn);
    }

    [Fact]
    public void ToProtocol_SupportedAuthenticationMethodsSchemaElement_ConvertsCorrectly()
    {
        var source = SupportedAuthenticationMethodsSchemaElement.Certificate;
        var result = source.ToProtocol();
        Assert.Equal(SupportedAuthenticationMethodsSchemaElementSchema.Certificate, result);
    }
}
