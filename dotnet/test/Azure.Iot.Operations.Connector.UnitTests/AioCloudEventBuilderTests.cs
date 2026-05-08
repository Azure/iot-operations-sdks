// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector.CloudEvents;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Xunit;

namespace Azure.Iot.Operations.Connector.UnitTests;

public class AioCloudEventBuilderTests
{
    #region Dataset Build Tests

    [Fact]
    public void Build_Dataset_WithAllParameters_ReturnsCorrectCloudEvent()
    {
        // Arrange
        var device = new Device
        {
            Uuid = "device-uuid-123",
            ExternalDeviceId = "ext-device-456"
        };

        var asset = new Asset
        {
            Uuid = "asset-uuid-789",
            ExternalAssetId = "ext-asset-012"
        };

        var dataset = new AssetDataset
        {
            Name = "dataset1",
            TypeRef = "telemetry",
            DataSource = "sub-source"
        };

        var messageSchemaRef = new MessageSchemaReference
        {
            SchemaRegistryNamespace = "test-namespace",
            SchemaName = "test-schema",
            SchemaVersion = "1.0.0"
        };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            "device-name",
            "endpoint1", asset, dataset, "asset-name", protocolSpecificIdentifier: "protocol-address", messageSchemaReference: messageSchemaRef);

        // Assert
        Assert.Equal(new Uri("ms-aio:protocol-address/sub-source"), result.Source);
        Assert.Equal("DataSet/telemetry", result.Type);
        Assert.Equal("ext-asset-012/dataset1", result.Subject);
        Assert.Equal("aio-sr://test-namespace/test-schema:1.0.0", result.DataSchema);
        Assert.Equal("ms-aio:device-uuid-123_endpoint1", result.AioDeviceRef);
        Assert.Equal("ms-aio:asset-uuid-789", result.AioAssetRef);
    }

    [Fact]
    public void Build_Dataset_WithMinimalParameters_ReturnsCorrectCloudEvent()
    {
        // Arrange
        var device = new Device
        {
            Uuid = "device-uuid-123"
        };

        var asset = new Asset
        {
            Uuid = "asset-uuid-789"
        };

        var dataset = new AssetDataset
        {
            Name = "dataset1"
        };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            deviceName: "device-name", endpointName: "endpoint1", protocolSpecificIdentifier: "protocol-address", asset: asset, dataset: dataset, assetName: "asset-name");

        // Assert
        Assert.Equal(new Uri("ms-aio:protocol-address"), result.Source);
        Assert.Equal("DataSet", result.Type);
        Assert.Equal("asset-name/dataset1", result.Subject);
        Assert.Null(result.DataSchema);
        Assert.Equal("ms-aio:device-uuid-123_endpoint1", result.AioDeviceRef);
        Assert.Equal("ms-aio:asset-uuid-789", result.AioAssetRef);
    }

    #endregion

    #region Event Build Tests

    [Fact]
    public void Build_Event_WithAllParameters_ReturnsCorrectCloudEvent()
    {
        // Arrange
        var device = new Device
        {
            Uuid = "device-uuid-123",
            ExternalDeviceId = "ext-device-456"
        };

        var asset = new Asset
        {
            Uuid = "asset-uuid-789",
            ExternalAssetId = "ext-asset-012"
        };

        var assetEvent = new AssetEvent
        {
            Name = "event1",
            TypeRef = "alarm",
            DataSource = "event-source"
        };

        var messageSchemaRef = new MessageSchemaReference
        {
            SchemaRegistryNamespace = "test-namespace",
            SchemaName = "event-schema",
            SchemaVersion = "2.0.0"
        };

        // Act
        var result = AioCloudEventBuilder.Build(
            device, "device-name", "endpoint1", asset, assetEvent, "asset-name", "eventGroup1", protocolSpecificIdentifier: "protocol-address", messageSchemaReference: messageSchemaRef);

        // Assert
        Assert.Equal(new Uri("ms-aio:protocol-address/event-source"), result.Source);
        Assert.Equal("Event/alarm", result.Type);
        Assert.Equal("ext-asset-012/eventGroup1/event1", result.Subject);
        Assert.Equal("aio-sr://test-namespace/event-schema:2.0.0", result.DataSchema);
        Assert.Equal("ms-aio:device-uuid-123_endpoint1", result.AioDeviceRef);
        Assert.Equal("ms-aio:asset-uuid-789", result.AioAssetRef);
    }

    [Fact]
    public void Build_Event_WithMinimalParameters_ReturnsCorrectCloudEvent()
    {
        // Arrange
        var device = new Device
        {
            Uuid = "device-uuid-123"
        };

        var asset = new Asset
        {
            Uuid = "asset-uuid-789"
        };

        var assetEvent = new AssetEvent
        {
            Name = "event1"
        };

        // Act
        var result = AioCloudEventBuilder.Build(
            device, deviceName: "device-name", endpointName: "endpoint1", protocolSpecificIdentifier: "protocol-address", asset: asset, assetEvent: assetEvent, assetName: "asset-name", eventGroupName: "eventGroup1");

        // Assert
        Assert.Equal(new Uri("ms-aio:protocol-address"), result.Source);
        Assert.Equal("Event", result.Type);
        Assert.Equal("asset-name/eventGroup1/event1", result.Subject);
        Assert.Null(result.DataSchema);
        Assert.Equal("ms-aio:device-uuid-123_endpoint1", result.AioDeviceRef);
        Assert.Equal("ms-aio:asset-uuid-789", result.AioAssetRef);
    }

    #endregion

    #region Source Generation Tests

    [Fact]
    public void Build_UsesProtocolAddress_ForSource()
    {
        // Arrange - Protocol specific identifier (endpoint address)
        var device = new Device
        {
            Uuid = "device-uuid",
            ExternalDeviceId = "ext-device-id"
        };
        var asset = new Asset { Uuid = "asset-uuid" };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            deviceName: "device-name", endpointName: "endpoint1", protocolSpecificIdentifier: "protocol-address", asset: asset, dataset: dataset, assetName: "asset-name");

        // Assert
        Assert.Equal(new Uri("ms-aio:protocol-address"), result.Source);
    }

    [Fact]
    public void Build_UsesExternalDeviceId_ForSource_WhenEndpointAddressIsNull()
    {
        // Arrange - External device ID (when different from UUID)
        var device = new Device
        {
            Uuid = "device-uuid",
            ExternalDeviceId = "ext-device-id"
        };
        var asset = new Asset { Uuid = "asset-uuid" };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            deviceName: "device-name", endpointName: "endpoint1", asset: asset, dataset: dataset, assetName: "asset-name");

        // Assert
        Assert.Equal(new Uri("ms-aio:ext-device-id"), result.Source);
    }

    [Fact]
    public void Build_UsesDeviceName_ForSource_WhenEndpointAddressIsNullAndExternalIdEqualsUuid()
    {
        // Arrange - Priority 4: Device name (when external ID equals UUID)
        var device = new Device
        {
            Uuid = "device-uuid-123",
            ExternalDeviceId = "device-uuid-123"
        };
        var asset = new Asset { Uuid = "asset-uuid" };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            deviceName: "device-name", endpointName: "endpoint1", asset: asset, dataset: dataset, assetName: "asset-name");

        // Assert
        Assert.Equal(new Uri("ms-aio:device-name"), result.Source);
    }

    [Fact]
    public void Build_AppendsDataSource_ToSource_WhenProvided()
    {
        // Arrange
        var device = new Device { Uuid = "device-uuid" };
        var asset = new Asset { Uuid = "asset-uuid" };
        var dataset = new AssetDataset
        {
            Name = "dataset1",
            DataSource = "subsource/path"
        };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            deviceName: "device-name", endpointName: "endpoint1", protocolSpecificIdentifier: "protocol-address", asset: asset, dataset: dataset, assetName: "asset-name");

        // Assert
        Assert.Equal(new Uri("ms-aio:protocol-address/subsource/path"), result.Source);
    }

    #endregion

    #region Subject Generation Tests

    [Fact]
    public void Build_UsesExternalAssetId_ForSubject_WhenDifferentFromUuid()
    {
        // Arrange - External asset ID (when different from UUID)
        var device = new Device { Uuid = "device-uuid" };
        var asset = new Asset
        {
            Uuid = "asset-uuid",
            ExternalAssetId = "ext-asset-id"
        };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            deviceName: "device-name", endpointName: "endpoint1", protocolSpecificIdentifier: "protocol-address", asset: asset, dataset: dataset, assetName: "asset-name");

        // Assert
        Assert.Equal("ext-asset-id/dataset1", result.Subject);
    }

    [Fact]
    public void Build_UsesAssetName_ForSubject_WhenExternalIdEqualsUuid()
    {
        // Arrange - Asset name (when external ID equals UUID)
        var device = new Device { Uuid = "device-uuid" };
        var asset = new Asset
        {
            Uuid = "asset-uuid-123",
            ExternalAssetId = "asset-uuid-123"
        };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            deviceName: "device-name", endpointName: "endpoint1", protocolSpecificIdentifier: "protocol-address", asset: asset, dataset: dataset, assetName: "asset-name");

        // Assert
        Assert.Equal("asset-name/dataset1", result.Subject);
    }

    #endregion

    #region Type Generation Tests

    [Fact]
    public void Build_Dataset_ReturnsTypeWithoutRef_WhenTypeRefIsNull()
    {
        // Arrange
        var device = new Device { Uuid = "device-uuid" };
        var asset = new Asset { Uuid = "asset-uuid" };
        var dataset = new AssetDataset
        {
            Name = "dataset1",
            TypeRef = null
        };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            deviceName: "device-name", endpointName: "endpoint1", protocolSpecificIdentifier: "protocol-address", asset: asset, dataset: dataset, assetName: "asset-name");

        // Assert
        Assert.Equal("DataSet", result.Type);
    }

    [Fact]
    public void Build_Dataset_ReturnsTypeWithRef_WhenTypeRefIsProvided()
    {
        // Arrange
        var device = new Device { Uuid = "device-uuid" };
        var asset = new Asset { Uuid = "asset-uuid" };
        var dataset = new AssetDataset
        {
            Name = "dataset1",
            TypeRef = "custom-type"
        };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            deviceName: "device-name", endpointName: "endpoint1", protocolSpecificIdentifier: "protocol-address", asset: asset, dataset: dataset, assetName: "asset-name");

        // Assert
        Assert.Equal("DataSet/custom-type", result.Type);
    }

    [Fact]
    public void Build_Event_ReturnsTypeWithoutRef_WhenTypeRefIsNull()
    {
        // Arrange
        var device = new Device { Uuid = "device-uuid" };
        var asset = new Asset { Uuid = "asset-uuid" };
        var assetEvent = new AssetEvent
        {
            Name = "event1",
            TypeRef = null
        };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            deviceName: "device-name", endpointName: "endpoint1", protocolSpecificIdentifier: "protocol-address", asset: asset, assetEvent: assetEvent, assetName: "asset-name", eventGroupName: "eventGroup1");

        // Assert
        Assert.Equal("Event", result.Type);
    }

    [Fact]
    public void Build_Event_ReturnsTypeWithRef_WhenTypeRefIsProvided()
    {
        // Arrange
        var device = new Device { Uuid = "device-uuid" };
        var asset = new Asset { Uuid = "asset-uuid" };
        var assetEvent = new AssetEvent
        {
            Name = "event1",
            TypeRef = "alarm-type"
        };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            "device-name", "endpoint1", asset, assetEvent, "asset-name", "eventGroup1", protocolSpecificIdentifier: "protocol-address");

        // Assert
        Assert.Equal("Event/alarm-type", result.Type);
    }

    #endregion

    #region AioDeviceRef Tests

    [Fact]
    public void Build_GeneratesCorrectAioDeviceRef()
    {
        // Arrange
        var device = new Device { Uuid = "device-uuid-123" };
        var asset = new Asset { Uuid = "asset-uuid" };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            "device-name", "endpoint-name", asset, dataset, "asset-name", protocolSpecificIdentifier: "protocol-address");

        // Assert
        Assert.Equal("ms-aio:device-uuid-123_endpoint-name", result.AioDeviceRef);
    }

    [Fact]
    public void Build_ThrowsInvalidOperationException_WhenDeviceUuidIsNull()
    {
        // Arrange
        var device = new Device { Uuid = null };
        var asset = new Asset { Uuid = "asset-uuid" };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            AioCloudEventBuilder.Build(
                device,
                "device-name", "endpoint1", asset, dataset, "asset-name"));

        Assert.Contains("Device UUID is required", ex.Message);
    }

    #endregion

    #region AioAssetRef Tests

    [Fact]
    public void Build_GeneratesCorrectAioAssetRef()
    {
        // Arrange
        var device = new Device { Uuid = "device-uuid" };
        var asset = new Asset { Uuid = "asset-uuid-456" };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            "device-name", "endpoint1", asset, dataset, "asset-name", protocolSpecificIdentifier: "protocol-address");

        // Assert
        Assert.Equal("ms-aio:asset-uuid-456", result.AioAssetRef);
    }

    [Fact]
    public void Build_ThrowsInvalidOperationException_WhenAssetUuidIsNull()
    {
        // Arrange
        var device = new Device { Uuid = "device-uuid" };
        var asset = new Asset { Uuid = null };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            AioCloudEventBuilder.Build(
                device,
                "device-name", "endpoint1", asset, dataset, "asset-name"));

        Assert.Contains("Asset UUID is required", ex.Message);
    }

    #endregion

    #region DataSchema Tests

    [Fact]
    public void Build_ReturnsNullDataSchema_WhenMessageSchemaRefIsNull()
    {
        // Arrange
        var device = new Device { Uuid = "device-uuid" };
        var asset = new Asset { Uuid = "asset-uuid" };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            "device-name",
            "endpoint1", asset, dataset, "asset-name", protocolSpecificIdentifier: "protocol-address", messageSchemaReference: null);

        // Assert
        Assert.Null(result.DataSchema);
    }

    [Fact]
    public void Build_GeneratesCorrectDataSchemaUri_WhenMessageSchemaRefIsProvided()
    {
        // Arrange
        var device = new Device { Uuid = "device-uuid" };
        var asset = new Asset { Uuid = "asset-uuid" };
        var dataset = new AssetDataset { Name = "dataset1" };
        var messageSchemaRef = new MessageSchemaReference
        {
            SchemaRegistryNamespace = "my-namespace",
            SchemaName = "my-schema",
            SchemaVersion = "3.2.1"
        };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            "device-name",
            "endpoint1", asset, dataset, "asset-name", protocolSpecificIdentifier: "protocol-address", messageSchemaReference: messageSchemaRef);

        // Assert
        Assert.Equal("aio-sr://my-namespace/my-schema:3.2.1", result.DataSchema);
    }

    #endregion

    #region UUID Comparison Tests

    [Fact]
    public void Build_TreatsExternalDeviceIdAsEqualToUuid_WhenBothAreIdentical()
    {
        // Arrange - External ID same as UUID should use device name instead
        var device = new Device
        {
            Uuid = "same-value",
            ExternalDeviceId = "same-value"
        };
        var asset = new Asset { Uuid = "asset-uuid" };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            deviceName: "device-name", endpointName: "endpoint1", asset: asset, dataset: dataset, assetName: "asset-name");

        // Assert - Should use device name (priority 4) not external ID
        Assert.Equal(new Uri("ms-aio:device-name"), result.Source);
    }

    [Fact]
    public void Build_TreatsExternalDeviceIdAsEqualToUuid_CaseInsensitive()
    {
        // Arrange - Case insensitive comparison
        var device = new Device
        {
            Uuid = "device-uuid-ABC",
            ExternalDeviceId = "device-uuid-abc"
        };
        var asset = new Asset { Uuid = "asset-uuid" };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            deviceName: "device-name", endpointName: "endpoint1", asset: asset, dataset: dataset, assetName: "asset-name");

        // Assert - Should use device name (priority 4) not external ID
        Assert.Equal(new Uri("ms-aio:device-name"), result.Source);
    }

    [Fact]
    public void Build_TreatsExternalDeviceIdAsEqualToUuid_WithDifferentFormatting()
    {
        // Arrange - UUID with different formatting (hyphens, braces)
        var device = new Device
        {
            Uuid = "123e4567-e89b-12d3-a456-426614174000",
            ExternalDeviceId = "{123e4567e89b12d3a456426614174000}"
        };
        var asset = new Asset { Uuid = "asset-uuid" };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            deviceName: "device-name", endpointName: "endpoint1", asset: asset, dataset: dataset, assetName: "asset-name");

        // Assert - Should use device name (priority 4) not external ID
        Assert.Equal(new Uri("ms-aio:device-name"), result.Source);
    }

    [Fact]
    public void Build_TreatsExternalAssetIdAsEqualToUuid_WhenBothAreIdentical()
    {
        // Arrange - External ID same as UUID should use asset name instead
        var device = new Device { Uuid = "device-uuid" };
        var asset = new Asset
        {
            Uuid = "same-value",
            ExternalAssetId = "same-value"
        };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            "device-name", "endpoint1", asset, dataset, "asset-name", protocolSpecificIdentifier: "protocol-address");

        // Assert - Should use asset name (priority 3) not external ID
        Assert.Equal("asset-name/dataset1", result.Subject);
    }

    [Fact]
    public void Build_UsesExternalDeviceId_WhenDifferentFromUuid()
    {
        // Arrange
        var device = new Device
        {
            Uuid = "device-uuid-123",
            ExternalDeviceId = "different-external-id"
        };
        var asset = new Asset { Uuid = "asset-uuid" };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            "device-name", "endpoint1", asset, dataset, "asset-name");

        // Assert - Should use external ID (priority 3)
        Assert.Equal(new Uri("ms-aio:different-external-id"), result.Source);
    }

    [Fact]
    public void Build_UsesExternalAssetId_WhenDifferentFromUuid()
    {
        // Arrange
        var device = new Device { Uuid = "device-uuid" };
        var asset = new Asset
        {
            Uuid = "asset-uuid-123",
            ExternalAssetId = "different-external-asset-id"
        };
        var dataset = new AssetDataset { Name = "dataset1" };

        // Act
        var result = AioCloudEventBuilder.Build(
            device,
            "device-name", "endpoint1", asset, dataset, "asset-name", protocolSpecificIdentifier: "protocol-address");

        // Assert - Should use external asset ID (priority 2)
        Assert.Equal("different-external-asset-id/dataset1", result.Subject);
    }

    #endregion
}
