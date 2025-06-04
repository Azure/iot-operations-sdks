// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.DeviceDiscoveryService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.ModelConverters;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using DeviceDiscoveryService = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.DeviceDiscoveryService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.ModelConverters;

internal static class ProtocolConverter
{
    public static DeviceDiscoveryService.DiscoveredDeviceInboundEndpointSchema ToProtocol(this Models.DiscoveredDeviceInboundEndpoint source)
    {
        return new()
        {
            AdditionalConfiguration = source.AdditionalConfiguration,
            Address = source.Address,
            SupportedAuthenticationMethods = source.SupportedAuthenticationMethods,
            EndpointType = source.EndpointType,
            Version = source.Version,
        };
    }

    public static DeviceDiscoveryService.DiscoveredDeviceEndpoints ToProtocol(this Models.DiscoveredDeviceEndpoints source)
    {
        return new()
        {
            Inbound = source.Inbound?.ToDictionary(x => x.Key, x => x.Value.ToProtocol()),
            Outbound = source.Outbound?.ToProtocol()
        };
    }

    public static DeviceDiscoveryService.DiscoveredDevice ToProtocol(this Models.DiscoveredDevice source)
    {
        return new DeviceDiscoveryService.DiscoveredDevice()
        {
            Attributes = source.Attributes,
            Endpoints = source.Endpoints?.ToProtocol(),
            ExternalDeviceId = source.ExternalDeviceId,
            Manufacturer = source.Manufacturer,
            Model = source.Model,
            OperatingSystem = source.OperatingSystem,
            OperatingSystemVersion = source.OperatingSystemVersion,
        };
    }

    public static AdrBaseService.GetAssetRequestPayload ToProtocol(this Models.GetAssetRequestPayload source)
    {
        return new AdrBaseService.GetAssetRequestPayload
        {
            AssetName = source.AssetName
        };
    }

    public static AdrBaseService.UpdateAssetStatusRequestPayload ToProtocol(this UpdateAssetStatusRequest source)
    {
        return new AdrBaseService.UpdateAssetStatusRequestPayload
        {
            AssetStatusUpdate = new UpdateAssetStatusRequestSchema
            {
                AssetName = source.AssetName,
                AssetStatus = source.AssetStatus.ToProtocol()
            }
        };
    }

    public static AdrBaseService.CreateOrUpdateDiscoveredAssetRequestPayload ToProtocol(this CreateOrUpdateDiscoveredAssetRequest source)
    {
        return new AdrBaseService.CreateOrUpdateDiscoveredAssetRequestPayload
        {
            DiscoveredAssetRequest = new CreateOrUpdateDiscoveredAssetRequestSchema
            {
                DiscoveredAsset = source.DiscoveredAsset.ToProtocol(),
                DiscoveredAssetName = source.DiscoveredAssetName
            }
        };
    }

    internal static AdrBaseService.DiscoveredAsset ToProtocol(this Models.DiscoveredAsset source)
    {
        return new AdrBaseService.DiscoveredAsset
        {
            AssetTypeRefs = source.AssetTypeRefs,
            Attributes = source.Attributes,
            Datasets = source.Datasets?.Select(x => x.ToProtocol()).ToList(),
            DefaultDatasetsConfiguration = source.DefaultDatasetsConfiguration?.RootElement.ToString(),
            DefaultDatasetsDestinations = source.DefaultDatasetsDestinations?.Select(x => x.ToProtocol()).ToList(),
            DefaultEventsConfiguration = source.DefaultEventsConfiguration?.RootElement.ToString(),
            DefaultEventsDestinations = source.DefaultEventsDestinations?.Select(x => x.ToProtocol()).ToList(),
            DefaultManagementGroupsConfiguration = source.DefaultManagementGroupsConfiguration?.RootElement.ToString(),
            DefaultStreamsConfiguration = source.DefaultStreamsConfiguration?.RootElement.ToString(),
            DefaultStreamsDestinations = source.DefaultStreamsDestinations?.Select(x => x.ToProtocol()).ToList(),
            DeviceRef = source.DeviceRef.ToProtocol(),
            DocumentationUri = source.DocumentationUri,
            Events = source.Events?.Select(x => x.ToProtocol()).ToList(),
            HardwareRevision = source.HardwareRevision,
            ManagementGroups = source.ManagementGroups?.Select(x => x.ToProtocol()).ToList(),
            Manufacturer = source.Manufacturer,
            ManufacturerUri = source.ManufacturerUri,
            Model = source.Model,
            ProductCode = source.ProductCode,
            SerialNumber = source.SerialNumber,
            SoftwareRevision = source.SoftwareRevision,
            Streams = source.Streams?.Select(x => x.ToProtocol()).ToList(),
        };
    }

    internal static AdrBaseService.DiscoveredAssetEvent ToProtocol(this Models.DiscoveredAssetEvent source)
    {
        return new AdrBaseService.DiscoveredAssetEvent
        {
            DataPoints = source.DataPoints?.Select(x => x.ToProtocol()).ToList(),
            Destinations = source.Destinations?.Select(x => x.ToProtocol()).ToList(),
            EventConfiguration = source.EventConfiguration?.RootElement.ToString(),
            EventNotifier = source.EventNotifier,
            LastUpdatedOn = source.LastUpdatedOn,
            Name = source.Name,
            TypeRef = source.TypeRef,
        };
    }

    internal static AdrBaseService.DiscoveredAssetEventDataPoint ToProtocol(this Models.DiscoveredAssetEventDataPoint source)
    {
        return new AdrBaseService.DiscoveredAssetEventDataPoint
        {
            DataPointConfiguration = source.DataPointConfiguration?.RootElement.ToString(),
            DataSource = source.DataSource,
            LastUpdatedOn = source.LastUpdatedOn,
            Name = source.Name,
        };
    }
    internal static AdrBaseService.DiscoveredAssetDataset ToProtocol(this Models.DiscoveredAssetDataset source)
    {
        return new AdrBaseService.DiscoveredAssetDataset
        {
            Name = source.Name,
            DatasetConfiguration = source.DatasetConfiguration?.RootElement.ToString(),
            LastUpdatedOn = source.LastUpdatedOn,
            Destinations = source.Destinations?.Select(x => x.ToProtocol()).ToList(),
            DataPoints = source.DataPoints?.Select(x => x.ToProtocol()).ToList(),
            DataSource = source.DataSource,
            TypeRef = source.TypeRef,
        };
    }

    internal static AdrBaseService.DiscoveredAssetDatasetDataPoint ToProtocol(this Models.DiscoveredAssetDatasetDataPoint source)
    {
        return new AdrBaseService.DiscoveredAssetDatasetDataPoint
        {
            Name = source.Name,
            DataPointConfiguration = source.DataPointConfiguration?.RootElement.ToString(),
            LastUpdatedOn = source.LastUpdatedOn,
            TypeRef = source.TypeRef,
            DataSource = source.DataSource
        };
    }

    internal static AdrBaseService.DiscoveredAssetStream ToProtocol(this Models.DiscoveredAssetStream source)
    {
        return new AdrBaseService.DiscoveredAssetStream
        {
            Destinations = source.Destinations?.Select(x => x.ToProtocol()).ToList(),
            LastUpdatedOn = source.LastUpdatedOn,
            Name = source.Name,
            TypeRef = source.TypeRef,
            StreamConfiguration = source.StreamConfiguration?.RootElement.ToString(),
        };
    }

    internal static AdrBaseService.DiscoveredAssetManagementGroup ToProtocol(this Models.DiscoveredAssetManagementGroup source)
{
    return new AdrBaseService.DiscoveredAssetManagementGroup
    {
        Actions = source.Actions?.Select(x => x.ToProtocol()).ToList(),
        DefaultTimeOutInSeconds = source.DefaultTimeOutInSeconds,
        DefaultTopic = source.DefaultTopic,
        LastUpdatedOn = source.LastUpdatedOn,
        ManagementGroupConfiguration = source.ManagementGroupConfiguration?.RootElement.ToString(),
        Name = source.Name,
        TypeRef = source.TypeRef,

    };
}

    internal static AdrBaseService.DiscoveredAssetManagementGroupAction ToProtocol(this Models.DiscoveredAssetManagementGroupAction source)
{
    return new AdrBaseService.DiscoveredAssetManagementGroupAction
    {
        Name = source.Name,
        ActionConfiguration = source.ActionConfiguration?.RootElement.ToString(),
        ActionType = source.ActionType.ToProtocol(),
        LastUpdatedOn = source.LastUpdatedOn,
        TypeRef = source.TypeRef,
        TargetUri = source.TargetUri,
        TimeOutInSeconds = source.TimeOutInSeconds,
        Topic = source.Topic,
    };
}

    internal static AdrBaseService. AssetManagementGroupActionType ToProtocol(this Models.AssetManagementGroupActionType source)
{
    return (AdrBaseService.AssetManagementGroupActionType)(int)source;
}

    internal static AdrBaseService.DatasetDestination ToProtocol(this Models.DatasetDestination source)
{
    return new AdrBaseService.DatasetDestination
    {
        Target = source.Target.ToProtocol(),
        Configuration = source.Configuration.ToProtocol()
    };
}

    internal static AdrBaseService.DatasetTarget ToProtocol(this Models.DatasetTarget source)
{
    return (AdrBaseService.DatasetTarget)(int)source;
}

    internal static AdrBaseService.EventStreamDestination ToProtocol(this Models.EventStreamDestination source)
{
    return new AdrBaseService.EventStreamDestination
    {
        Configuration = source.Configuration.ToProtocol(),
        Target = source.Target.ToProtocol(),
    };
}

    internal static AdrBaseService.EventStreamTarget ToProtocol(this Models.EventStreamTarget source)
{
    return (AdrBaseService.EventStreamTarget)(int)source;
}

    internal static AdrBaseService.DestinationConfiguration ToProtocol(this Models.DestinationConfiguration source)
{
    return new AdrBaseService.DestinationConfiguration
    {
        Key = source.Key,
        Path = source.Path,
        Topic = source.Topic,
        Qos = source.Qos?.ToProtocol(),
        Retain = source.Retain?.ToProtocol(),
        Ttl = source.Ttl
    };
}

    internal static AdrBaseService.Qos ToProtocol(this Models.Qos source)
{
    return (AdrBaseService.Qos)(int)source;
}

    internal static AdrBaseService.AssetDeviceRef ToProtocol(this Models.AssetDeviceRef source)
    {
        return new AdrBaseService.AssetDeviceRef
        {
            DeviceName = source.DeviceName,
            EndpointName = source.EndpointName
        };
    }

    internal static  DiscoveredDeviceOutboundEndpointsSchema ToProtocol(this DiscoveredDeviceOutboundEndpoints source)
    {
        return new DiscoveredDeviceOutboundEndpointsSchema
        {
            Assigned = source.Assigned.ToDictionary(x => x.Key, x => x.Value.ToProtocol())
        };
    }

    internal static DeviceDiscoveryService.DeviceOutboundEndpoint ToProtocol(this Models.DeviceOutboundEndpoint source)
    {
        return new DeviceDiscoveryService.DeviceOutboundEndpoint
        {
            Address = source.Address,
            EndpointType = source.EndpointType,
        };
    }
    public static AdrBaseService.DeviceStatus ToProtocol(this Models.DeviceStatus source)
    {
        return new AdrBaseService.DeviceStatus
        {
            Config = source.Config?.ToProtocol(),
            Endpoints = source.Endpoints?.ToProtocol(),
        };
    }

    internal static AdrBaseService.AssetStatus ToProtocol(this Models.AssetStatus source)
    {
        return new AdrBaseService.AssetStatus
        {
            Config = source.Config?.ToProtocol(),
            Datasets = source.Datasets?.Select(x => x.ToProtocol()).ToList(),
            Events = source.Events?.Select(x => x.ToProtocol()).ToList(),
            ManagementGroups = source.ManagementGroups?.Select(x => x.ToProtocol()).ToList(),
            Streams = source.Streams?.Select(x => x.ToProtocol()).ToList()
        };
    }

    internal static AssetManagementGroupStatusSchemaElementSchema ToProtocol(this AssetManagementGroupStatusSchemaElement source)
    {
        return new AssetManagementGroupStatusSchemaElementSchema
        {
            Name = source.Name,
            Actions = source.Actions?.Select(x => x.ToProtocol()).ToList()
        };
    }

    internal static AssetManagementGroupActionStatusSchemaElementSchema ToProtocol(this AssetManagementGroupActionStatusSchemaElement source)
    {
        return new AssetManagementGroupActionStatusSchemaElementSchema
        {
            Error = source.Error?.ToProtocol(),
            Name = source.Name,
            RequestMessageSchemaReference = source.RequestMessageSchemaReference?.ToProtocol(),
            ResponseMessageSchemaReference = source.ResponseMessageSchemaReference?.ToProtocol()
        };
    }

    internal static AssetConfigStatusSchema ToProtocol(this AssetConfigStatus source)
    {
        return new AssetConfigStatusSchema
        {
            Error = source.Error?.ToProtocol(),
            LastTransitionTime = source.LastTransitionTime,
            Version = source.Version
        };
    }

    internal static AdrBaseService.AssetDatasetEventStreamStatus ToProtocol(this Models.AssetDatasetEventStreamStatus source)
    {
        return new AdrBaseService.AssetDatasetEventStreamStatus
        {
            Error = source.Error?.ToProtocol(),
            Name = source.Name,
            MessageSchemaReference = source.MessageSchemaReference?.ToProtocol()
        };
    }

    internal static AdrBaseService.MessageSchemaReference ToProtocol(this Models.MessageSchemaReference source)
    {
        return new AdrBaseService.MessageSchemaReference
        {
            SchemaName = source.SchemaName,
            SchemaRegistryNamespace = source.SchemaRegistryNamespace,
            SchemaVersion = source.SchemaVersion,
        };
    }

    internal static AdrBaseService.Retain ToProtocol(this Models.Retain source)
    {
        return (AdrBaseService.Retain)(int)source;
    }

    internal static DeviceStatusConfigSchema ToProtocol(this DeviceStatusConfig source)
    {
        return new DeviceStatusConfigSchema
        {
            Error = source.Error?.ToProtocol(),
            LastTransitionTime = source.LastTransitionTime,
            Version = source.Version
        };
    }

    internal static DeviceStatusEndpointSchema ToProtocol(this DeviceStatusEndpoint source)
    {
        return new DeviceStatusEndpointSchema
        {
            Inbound = source.Inbound?.ToDictionary(x => x.Key, x => x.Value.ToProtocol()),
        };
    }

    internal static DeviceStatusInboundEndpointSchemaMapValueSchema ToProtocol(this DeviceStatusInboundEndpointSchemaMapValue source)
    {
        return new DeviceStatusInboundEndpointSchemaMapValueSchema
        {
            Error = source.Error?.ToProtocol()
        };
    }

    internal static AdrBaseService.ConfigError ToProtocol(this Models.ConfigError source)
    {
        return new AdrBaseService.ConfigError
        {
            Code = source.Code,
            Message = source.Message,
            Details = source.Details?.Select(x => x.ToProtocol()).ToList(),
            InnerError = source.InnerError
        };
    }

    internal static DetailsSchemaElementSchema ToProtocol(this DetailsSchemaElement source)
    {
        return new DetailsSchemaElementSchema
        {
            Code = source.Code,
            Message = source.Message,
            CorrelationId = source.CorrelationId,
            Info = source.Info
        };
    }
}
