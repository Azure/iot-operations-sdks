// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Generated.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Generated.DeviceDiscoveryService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

internal static class ProtocolConverter
{
    public static GetAssetRequestPayload ToProtocol(this GetAssetRequest source)
    {
        return new GetAssetRequestPayload
        {
            AssetName = source.AssetName
        };
    }

    public static Generated.DeviceDiscoveryService.DiscoveredDevice ToProtocol(this Models.DiscoveredDevice source)
    {
        return new()
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

    public static Generated.DeviceDiscoveryService.CreateOrUpdateDiscoveredDeviceRequestSchema ToProtocol(this Models.CreateOrUpdateDiscoveredDeviceRequestSchema source)
    {
        return new()
        {
            DiscoveredDevice = source.DiscoveredDevice.ToProtocol(),
            DiscoveredDeviceName = source.DiscoveredDeviceName,
        };
    }

    public static UpdateAssetStatusRequestPayload ToProtocol(this UpdateAssetStatusRequest source)
    {
        return new UpdateAssetStatusRequestPayload
        {
            AssetStatusUpdate = new UpdateAssetStatusRequestSchema
            {
                AssetName = source.AssetName,
                AssetStatus = source.AssetStatus.ToProtocol()
            }
        };
    }

    public static CreateOrUpdateDiscoveredAssetRequestPayload ToProtocol(this CreateOrUpdateDiscoveredAssetRequest source)
    {
        return new CreateOrUpdateDiscoveredAssetRequestPayload
        {
            DiscoveredAssetRequest = new CreateOrUpdateDiscoveredAssetRequestSchema
            {
                DiscoveredAsset = source.DiscoveredAsset.ToProtocol(),
                DiscoveredAssetName = source.DiscoveredAssetName
            }
        };
    }

    internal static Generated.AdrBaseService.DiscoveredAssetEventGroup ToProtocol(this DiscoveredAssetEventGroup source)
    {
        return new Generated.AdrBaseService.DiscoveredAssetEventGroup()
        {
            DataSource = source.DataSource,
            DefaultEventsDestinations = source.DefaultEventsDestinations?.Select(x => x.ToProtocol()).ToList(),
            EventGroupConfiguration = source.EventGroupConfiguration,
            Events = source.Events?.Select(x => x.ToProtocol()).ToList(),
            Name = source.Name,
            TypeRef = source.TypeRef,
        };
    }

    internal static Generated.AdrBaseService.DiscoveredAsset ToProtocol(this DiscoveredAsset source)
    {
        return new Generated.AdrBaseService.DiscoveredAsset
        {
            AssetTypeRefs = source.AssetTypeRefs,
            Attributes = source.Attributes,
            Datasets = source.Datasets?.Select(x => x.ToProtocol()).ToList(),
            DefaultDatasetsConfiguration = source.DefaultDatasetsConfiguration,
            DefaultDatasetsDestinations = source.DefaultDatasetsDestinations?.Select(x => x.ToProtocol()).ToList(),
            DefaultEventsConfiguration = source.DefaultEventsConfiguration,
            DefaultEventsDestinations = source.DefaultEventsDestinations?.Select(x => x.ToProtocol()).ToList(),
            DefaultManagementGroupsConfiguration = source.DefaultManagementGroupsConfiguration,
            DefaultStreamsConfiguration = source.DefaultStreamsConfiguration,
            DefaultStreamsDestinations = source.DefaultStreamsDestinations?.Select(x => x.ToProtocol()).ToList(),
            Description = source.Description,
            DeviceRef = source.DeviceRef.ToProtocol(),
            DisplayName = source.DisplayName,
            DocumentationUri = source.DocumentationUri,
            EventGroups = source.EventGroups?.Select(x => x.ToProtocol()).ToList(),
            ExternalAssetId = source.ExternalAssetId,
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

    internal static Generated.AdrBaseService.DiscoveredAssetEvent ToProtocol(this DiscoveredAssetEvent source)
    {
        return new Generated.AdrBaseService.DiscoveredAssetEvent
        {
            Destinations = source.Destinations?.Select(x => x.ToProtocol()).ToList(),
            EventConfiguration = source.EventConfiguration,
            DataSource = source.DataSource,
            LastUpdatedOn = source.LastUpdatedOn,
            Name = source.Name,
            TypeRef = source.TypeRef,
        };
    }

    internal static Generated.AdrBaseService.DiscoveredAssetDataset ToProtocol(this DiscoveredAssetDataset source)
    {
        return new Generated.AdrBaseService.DiscoveredAssetDataset
        {
            Name = source.Name,
            DatasetConfiguration = source.DataSetConfiguration,
            LastUpdatedOn = source.LastUpdatedOn,
            Destinations = source.Destinations?.Select(x => x.ToProtocol()).ToList(),
            DataPoints = source.DataPoints?.Select(x => x.ToProtocol()).ToList(),
            DataSource = source.DataSource,
            TypeRef = source.TypeRef,
        };
    }

    internal static Generated.AdrBaseService.DiscoveredAssetDatasetDataPoint ToProtocol(this DiscoveredAssetDatasetDataPoint source)
    {
        return new Generated.AdrBaseService.DiscoveredAssetDatasetDataPoint
        {
            Name = source.Name,
            DataPointConfiguration = source.DataPointConfiguration,
            LastUpdatedOn = source.LastUpdatedOn,
            TypeRef = source.TypeRef,
            DataSource = source.DataSource
        };
    }

    internal static Generated.AdrBaseService.DiscoveredAssetStream ToProtocol(this DiscoveredAssetStream source)
    {
        return new Generated.AdrBaseService.DiscoveredAssetStream
        {
            Destinations = source.Destinations?.Select(x => x.ToProtocol()).ToList(),
            LastUpdatedOn = source.LastUpdatedOn,
            Name = source.Name,
            TypeRef = source.TypeRef,
            StreamConfiguration = source.StreamConfiguration,
        };
    }

    internal static Generated.AdrBaseService.DiscoveredAssetManagementGroup ToProtocol(this DiscoveredAssetManagementGroup source)
{
    return new Generated.AdrBaseService.DiscoveredAssetManagementGroup
    {
        Actions = source.Actions?.Select(x => x.ToProtocol()).ToList(),
        DataSource = source.DataSource,
        DefaultTimeoutInSeconds = source.DefaultTimeoutInSeconds,
        DefaultTopic = source.DefaultTopic,
        LastUpdatedOn = source.LastUpdatedOn,
        ManagementGroupConfiguration = source.ManagementGroupConfiguration,
        Name = source.Name,
        TypeRef = source.TypeRef,
    };
}

    internal static Generated.AdrBaseService.DiscoveredAssetManagementGroupAction ToProtocol(this DiscoveredAssetManagementGroupAction source)
{
    return new Generated.AdrBaseService.DiscoveredAssetManagementGroupAction
    {
        Name = source.Name,
        ActionConfiguration = source.ActionConfiguration,
        ActionType = source.ActionType.ToProtocol(),
        LastUpdatedOn = source.LastUpdatedOn,
        TypeRef = source.TypeRef,
        TargetUri = source.TargetUri,
        TimeoutInSeconds = source.TimeoutInSeconds,
        Topic = source.Topic,
    };
}

    internal static Generated.AdrBaseService. AssetManagementGroupActionType ToProtocol(this AssetManagementGroupActionType source)
{
    return (Generated.AdrBaseService.AssetManagementGroupActionType)(int)source;
}

    internal static Generated.AdrBaseService.DatasetDestination ToProtocol(this DatasetDestination source)
{
    return new Generated.AdrBaseService.DatasetDestination
    {
        Target = source.Target.ToProtocol(),
        Configuration = source.Configuration.ToProtocol()
    };
}

    internal static Generated.AdrBaseService.DatasetTarget ToProtocol(this DatasetTarget source)
{
    return (Generated.AdrBaseService.DatasetTarget)(int)source;
}

    internal static Generated.AdrBaseService.EventStreamDestination ToProtocol(this EventStreamDestination source)
{
    return new Generated.AdrBaseService.EventStreamDestination
    {
        Configuration = source.Configuration.ToProtocol(),
        Target = source.Target.ToProtocol(),
    };
}

    internal static Generated.AdrBaseService.EventStreamTarget ToProtocol(this EventStreamTarget source)
{
    return (Generated.AdrBaseService.EventStreamTarget)(int)source;
}

    internal static Generated.AdrBaseService.DestinationConfiguration ToProtocol(this DestinationConfiguration source)
{
    return new Generated.AdrBaseService.DestinationConfiguration
    {
        Key = source.Key,
        Path = source.Path,
        Topic = source.Topic,
        Qos = source.Qos?.ToProtocol(),
        Retain = source.Retain?.ToProtocol(),
        Ttl = source.Ttl
    };
}

    internal static Generated.AdrBaseService.Qos ToProtocol(this QoS source)
{
    return (Generated.AdrBaseService.Qos)(int)source;
}

    internal static Generated.AdrBaseService.AssetDeviceRef ToProtocol(this AssetDeviceRef source)
    {
        return new Generated.AdrBaseService.AssetDeviceRef
        {
            DeviceName = source.DeviceName,
            EndpointName = source.EndpointName
        };
    }

    internal static Generated.DeviceDiscoveryService.DiscoveredDeviceEndpoints ToProtocol(this Models.DiscoveredDeviceEndpoints source)
    {
        return new Generated.DeviceDiscoveryService.DiscoveredDeviceEndpoints
        {
            Inbound = source.Inbound?.ToDictionary(x => x.Key, x => x.Value.ToProtocol()),
            Outbound = source.Outbound?.ToProtocol(),
        };
    }

    internal static  DiscoveredDeviceOutboundEndpointsSchema ToProtocol(this Models.DiscoveredDeviceOutboundEndpoints source)
    {
        return new DiscoveredDeviceOutboundEndpointsSchema
        {
            Assigned = source.Assigned.ToDictionary(x => x.Key, x => x.Value.ToProtocol())
        };
    }

    internal static Generated.DeviceDiscoveryService.DeviceOutboundEndpoint ToProtocol(this Models.DeviceOutboundEndpoint source)
    {
        return new Generated.DeviceDiscoveryService.DeviceOutboundEndpoint
        {
            Address = source.Address,
            EndpointType = source.EndpointType,
        };
    }

    internal static DiscoveredDeviceInboundEndpointSchema ToProtocol(this DiscoveredDeviceInboundEndpoint source)
    {
        return new DiscoveredDeviceInboundEndpointSchema
        {
            AdditionalConfiguration = source.AdditionalConfiguration,
            Address = source.Address,
            EndpointType = source.EndpointType,
            SupportedAuthenticationMethods = source.SupportedAuthenticationMethods,
            Version = source.Version
        };
    }

    public static Generated.AdrBaseService.DeviceStatus ToProtocol(this DeviceStatus source)
    {
        return new Generated.AdrBaseService.DeviceStatus
        {
            Config = source.Config?.ToProtocol(),
            Endpoints = source.Endpoints?.ToProtocol(),
        };
    }

    internal static Generated.AdrBaseService.ConfigStatus ToProtocol(this ConfigStatus source)
    {
        return new()
        {
            Error = source.Error?.ToProtocol(),
            LastTransitionTime = source.LastTransitionTime,
            Version = source.Version,
        };
    }

    internal static Generated.AdrBaseService.AssetStatus ToProtocol(this AssetStatus source)
    {
        return new Generated.AdrBaseService.AssetStatus
        {
            Config = source.Config?.ToProtocol(),
            Datasets = source.Datasets?.Select(x => x.ToProtocol()).ToList(),
            EventGroups = source.EventGroups?.Select(x => x.ToProtocol()).ToList(),
            ManagementGroups = source.ManagementGroups?.Select(x => x.ToProtocol()).ToList(),
            Streams = source.Streams?.Select(x => x.ToProtocol()).ToList()
        };
    }

    internal static Generated.AdrBaseService.AssetEventGroupStatusSchemaElementSchema ToProtocol(this AssetEventGroupStatus source)
    {
        return new Generated.AdrBaseService.AssetEventGroupStatusSchemaElementSchema
        {
            Events = source.Events?.Select(x => x.ToProtocol()).ToList(),
            Name = source.Name
        };
    }

    internal static AssetManagementGroupStatusSchemaElementSchema ToProtocol(this AssetManagementGroupStatus source)
    {
        return new AssetManagementGroupStatusSchemaElementSchema
        {
            Name = source.Name,
            Actions = source.Actions?.Select(x => x.ToProtocol()).ToList()
        };
    }

    internal static AssetManagementGroupActionStatusSchemaElementSchema ToProtocol(this AssetManagementGroupActionStatus source)
    {
        return new AssetManagementGroupActionStatusSchemaElementSchema
        {
            Error = source.Error?.ToProtocol(),
            Name = source.Name,
            RequestMessageSchemaReference = source.RequestMessageSchemaReference?.ToProtocol(),
            ResponseMessageSchemaReference = source.ResponseMessageSchemaReference?.ToProtocol()
        };
    }

    internal static Generated.AdrBaseService.AssetDatasetEventStreamStatus ToProtocol(this AssetDatasetEventStreamStatus source)
    {
        return new Generated.AdrBaseService.AssetDatasetEventStreamStatus
        {
            Error = source.Error?.ToProtocol(),
            Name = source.Name,
            MessageSchemaReference = source.MessageSchemaReference?.ToProtocol()
        };
    }

    internal static Generated.AdrBaseService.MessageSchemaReference ToProtocol(this MessageSchemaReference source)
    {
        return new Generated.AdrBaseService.MessageSchemaReference
        {
            SchemaName = source.SchemaName,
            SchemaRegistryNamespace = source.SchemaRegistryNamespace,
            SchemaVersion = source.SchemaVersion,
        };
    }

    internal static Generated.AdrBaseService.Retain ToProtocol(this Retain source)
    {
        return (Generated.AdrBaseService.Retain)(int)source;
    }

    internal static DeviceStatusEndpointSchema ToProtocol(this DeviceStatusEndpoint source)
    {
        return new DeviceStatusEndpointSchema
        {
            Inbound = new Dictionary<string, DeviceStatusInboundEndpointSchemaMapValueSchema>(
                source.Inbound?.Select(x => new KeyValuePair<string, DeviceStatusInboundEndpointSchemaMapValueSchema>(x.Key, x.Value.ToProtocol())) ??
                new Dictionary<string, DeviceStatusInboundEndpointSchemaMapValueSchema>())
        };
    }

    internal static DeviceStatusInboundEndpointSchemaMapValueSchema ToProtocol(this DeviceStatusInboundEndpointSchemaMapValue source)
    {
        return new DeviceStatusInboundEndpointSchemaMapValueSchema
        {
            Error = source.Error?.ToProtocol()
        };
    }

    internal static Generated.AdrBaseService.ConfigError ToProtocol(this ConfigError source)
    {
        return new Generated.AdrBaseService.ConfigError
        {
            Code = source.Code,
            Message = source.Message,
            Details = source.Details?.Select(x => x.ToProtocol()).ToList(),
        };
    }

    internal static Generated.AdrBaseService.DetailsSchemaElementSchema ToProtocol(this ConfigErrorDetails source)
    {
        return new Generated.AdrBaseService.DetailsSchemaElementSchema
        {
            Code = source.Code,
            Message = source.Message,
            CorrelationId = source.CorrelationId,
            Info = source.Info
        };
    }
}
