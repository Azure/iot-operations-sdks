// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.DeviceDiscoveryService;
using DeviceDiscoveryService = Azure.Iot.Operations.Services.AssetAndDeviceRegistry.DeviceDiscoveryService;

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

    public static DeviceDiscoveryService.DiscoveredDevice ToProtocol(this Models.DiscoveredDevice source)
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

    public static DeviceDiscoveryService.CreateOrUpdateDiscoveredDeviceRequestSchema ToProtocol(this Models.CreateOrUpdateDiscoveredDeviceRequestSchema source)
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

    internal static AdrBaseService.DiscoveredAssetEventGroup ToProtocol(this DiscoveredAssetEventGroup source)
    {
        return new AdrBaseService.DiscoveredAssetEventGroup()
        {
            DataSource = source.DataSource,
            DefaultDestinations = source.DefaultDestinations?.Select(x => x.ToProtocol()).ToList(),
            EventGroupConfiguration = source.EventGroupConfiguration,
            Events = source.Events?.Select(x => x.ToProtocol()).ToList(),
            Name = source.Name,
            TypeRef = source.TypeRef,
        };
    }

    internal static AdrBaseService.DiscoveredAsset ToProtocol(this DiscoveredAsset source)
    {
        return new AdrBaseService.DiscoveredAsset
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
            DeviceRef = source.DeviceRef.ToProtocol(),
            DocumentationUri = source.DocumentationUri,
            EventGroups = source.EventGroups?.Select(x => x.ToProtocol()).ToList(),
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

    internal static AdrBaseService.DiscoveredAssetEvent ToProtocol(this DiscoveredAssetEvent source)
    {
        return new AdrBaseService.DiscoveredAssetEvent
        {
            Destinations = source.Destinations?.Select(x => x.ToProtocol()).ToList(),
            EventConfiguration = source.EventConfiguration,
            DataSource = source.DataSource,
            LastUpdatedOn = source.LastUpdatedOn,
            Name = source.Name,
            TypeRef = source.TypeRef,
        };
    }

    internal static AdrBaseService.DiscoveredAssetDataset ToProtocol(this DiscoveredAssetDataset source)
    {
        return new AdrBaseService.DiscoveredAssetDataset
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

    internal static AdrBaseService.DiscoveredAssetDatasetDataPoint ToProtocol(this DiscoveredAssetDatasetDataPoint source)
    {
        return new AdrBaseService.DiscoveredAssetDatasetDataPoint
        {
            Name = source.Name,
            DataPointConfiguration = source.DataPointConfiguration,
            LastUpdatedOn = source.LastUpdatedOn,
            TypeRef = source.TypeRef,
            DataSource = source.DataSource
        };
    }

    internal static AdrBaseService.DiscoveredAssetStream ToProtocol(this DiscoveredAssetStream source)
    {
        return new AdrBaseService.DiscoveredAssetStream
        {
            Destinations = source.Destinations?.Select(x => x.ToProtocol()).ToList(),
            LastUpdatedOn = source.LastUpdatedOn,
            Name = source.Name,
            TypeRef = source.TypeRef,
            StreamConfiguration = source.StreamConfiguration,
        };
    }

    internal static AdrBaseService.DiscoveredAssetManagementGroup ToProtocol(this DiscoveredAssetManagementGroup source)
{
    return new AdrBaseService.DiscoveredAssetManagementGroup
    {
        Actions = source.Actions?.Select(x => x.ToProtocol()).ToList(),
        DefaultTimeoutInSeconds = source.DefaultTimeoutInSeconds,
        DefaultTopic = source.DefaultTopic,
        LastUpdatedOn = source.LastUpdatedOn,
        ManagementGroupConfiguration = source.ManagementGroupConfiguration,
        Name = source.Name,
        TypeRef = source.TypeRef,

    };
}

    internal static AdrBaseService.DiscoveredAssetManagementGroupAction ToProtocol(this DiscoveredAssetManagementGroupAction source)
{
    return new AdrBaseService.DiscoveredAssetManagementGroupAction
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

    internal static AdrBaseService. AssetManagementGroupActionType ToProtocol(this AssetManagementGroupActionType source)
{
    return (AdrBaseService.AssetManagementGroupActionType)(int)source;
}

    internal static AdrBaseService.DatasetDestination ToProtocol(this DatasetDestination source)
{
    return new AdrBaseService.DatasetDestination
    {
        Target = source.Target.ToProtocol(),
        Configuration = source.Configuration.ToProtocol()
    };
}

    internal static AdrBaseService.DatasetTarget ToProtocol(this DatasetTarget source)
{
    return (AdrBaseService.DatasetTarget)(int)source;
}

    internal static AdrBaseService.EventStreamDestination ToProtocol(this EventStreamDestination source)
{
    return new AdrBaseService.EventStreamDestination
    {
        Configuration = source.Configuration.ToProtocol(),
        Target = source.Target.ToProtocol(),
    };
}

    internal static AdrBaseService.EventStreamTarget ToProtocol(this EventStreamTarget source)
{
    return (AdrBaseService.EventStreamTarget)(int)source;
}

    internal static AdrBaseService.DestinationConfiguration ToProtocol(this DestinationConfiguration source)
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

    internal static AdrBaseService.Qos ToProtocol(this QoS source)
{
    return (AdrBaseService.Qos)(int)source;
}

    internal static AdrBaseService.AssetDeviceRef ToProtocol(this AssetDeviceRef source)
    {
        return new AdrBaseService.AssetDeviceRef
        {
            DeviceName = source.DeviceName,
            EndpointName = source.EndpointName
        };
    }

    internal static DeviceDiscoveryService.DiscoveredDeviceEndpoints ToProtocol(this Models.DiscoveredDeviceEndpoints source)
    {
        return new DeviceDiscoveryService.DiscoveredDeviceEndpoints
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

    internal static DeviceDiscoveryService.DeviceOutboundEndpoint ToProtocol(this Models.DeviceOutboundEndpoint source)
    {
        return new DeviceDiscoveryService.DeviceOutboundEndpoint
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

    public static AdrBaseService.DeviceStatus ToProtocol(this DeviceStatus source)
    {
        return new AdrBaseService.DeviceStatus
        {
            Config = source.Config?.ToProtocol(),
            Endpoints = source.Endpoints?.ToProtocol(),
        };
    }

    internal static AdrBaseService.ConfigStatus ToProtocol(this ConfigStatus source)
    {
        return new()
        {
            Error = source.Error?.ToProtocol(),
            LastTransitionTime = source.LastTransitionTime,
            Version = source.Version,
        };
    }

    internal static AdrBaseService.AssetStatus ToProtocol(this AssetStatus source)
    {
        return new AdrBaseService.AssetStatus
        {
            Config = source.Config?.ToProtocol(),
            Datasets = source.Datasets?.Select(x => x.ToProtocol()).ToList(),
            EventGroups = source.EventGroups?.Select(x => x.ToProtocol()).ToList(),
            ManagementGroups = source.ManagementGroups?.Select(x => x.ToProtocol()).ToList(),
            Streams = source.Streams?.Select(x => x.ToProtocol()).ToList()
        };
    }

    internal static AdrBaseService.AssetEventGroupStatusSchemaElementSchema ToProtocol(this AssetEventGroupStatus source)
    {
        return new AdrBaseService.AssetEventGroupStatusSchemaElementSchema
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

    internal static AdrBaseService.AssetDatasetEventStreamStatus ToProtocol(this AssetDatasetEventStreamStatus source)
    {
        return new AdrBaseService.AssetDatasetEventStreamStatus
        {
            Error = source.Error?.ToProtocol(),
            Name = source.Name,
            MessageSchemaReference = source.MessageSchemaReference?.ToProtocol()
        };
    }

    internal static AdrBaseService.MessageSchemaReference ToProtocol(this MessageSchemaReference source)
    {
        return new AdrBaseService.MessageSchemaReference
        {
            SchemaName = source.SchemaName,
            SchemaRegistryNamespace = source.SchemaRegistryNamespace,
            SchemaVersion = source.SchemaVersion,
        };
    }

    internal static AdrBaseService.Retain ToProtocol(this Retain source)
    {
        return (AdrBaseService.Retain)(int)source;
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

    internal static AdrBaseService.ConfigError ToProtocol(this ConfigError source)
    {
        return new AdrBaseService.ConfigError
        {
            Code = source.Code,
            Message = source.Message,
            Details = source.Details?.Select(x => x.ToProtocol()).ToList(),
        };
    }

    internal static AdrBaseService.DetailsSchemaElementSchema ToProtocol(this ConfigErrorDetails source)
    {
        return new AdrBaseService.DetailsSchemaElementSchema
        {
            Code = source.Code,
            Message = source.Message,
            CorrelationId = source.CorrelationId,
            Info = source.Info
        };
    }

    internal static AdrBaseService.DatasetsSchemaElementSchema ToProtocol(this DatasetsSchemaElementSchema source)
    {
        return new AdrBaseService.DatasetsSchemaElementSchema
        {
            DatasetName = source.DatasetName,
            RuntimeHealth = source.RuntimeHealth.ToProtocol(),
        };
    }

    internal static AdrBaseService.EventsSchemaElementSchema ToProtocol(this EventsSchemaElementSchema source)
    {
        return new AdrBaseService.EventsSchemaElementSchema
        {
            EventGroupName = source.EventGroupName,
            EventName = source.EventName,
            RuntimeHealth = source.RuntimeHealth.ToProtocol(),
        };
    }

    internal static AdrBaseService.ManagementActionsSchemaElementSchema ToProtocol(this ManagementActionsSchemaElementSchema source)
    {
        return new AdrBaseService.ManagementActionsSchemaElementSchema
        {
            ManagementActionName = source.ManagementActionName,
            ManagementGroupName = source.ManagementGroupName,
            RuntimeHealth = source.RuntimeHealth.ToProtocol(),
        };
    }

    internal static AdrBaseService.StreamsSchemaElementSchema ToProtocol(this StreamsSchemaElementSchema source)
    {
        return new AdrBaseService.StreamsSchemaElementSchema
        {
            StreamName = source.StreamName,
            RuntimeHealth = source.RuntimeHealth.ToProtocol(),
        };
    }

    internal static AdrBaseService.StreamRuntimeHealthEventSchema ToProtocol(this StreamRuntimeHealthEventSchema source)
    {
        var streams = source.Streams?.Select(x => x.ToProtocol());
        return new AdrBaseService.StreamRuntimeHealthEventSchema
        {
            AssetName = source.AssetName,
            Streams = streams != null ?  streams.ToList() : new(),
        };
    }

    internal static AdrBaseService.ManagementActionRuntimeHealthEventSchema ToProtocol(this ManagementActionRuntimeHealthEventSchema source)
    {
        var managementActions = source.ManagementActions?.Select(x => x.ToProtocol());
        return new AdrBaseService.ManagementActionRuntimeHealthEventSchema
        {
            AssetName = source.AssetName,
            ManagementActions = managementActions != null ? managementActions.ToList() : new(),
        };
    }

    internal static AdrBaseService.EventRuntimeHealthEventSchema ToProtocol(this EventRuntimeHealthEventSchema source)
    {
        var events = source.Events?.Select(x => x.ToProtocol());
        return new AdrBaseService.EventRuntimeHealthEventSchema
        {
            AssetName = source.AssetName,
            Events = events != null ? events.ToList() : new(),
        };
    }

    internal static AdrBaseService.DatasetRuntimeHealthEventSchema ToProtocol(this DatasetRuntimeHealthEventSchema source)
    {
        var datasets = source.Datasets?.Select(x => x.ToProtocol());
        return new AdrBaseService.DatasetRuntimeHealthEventSchema
        {
            AssetName = source.AssetName,
            Datasets = datasets != null ? datasets.ToList() : new(),
        };
    }

    internal static AdrBaseService.DeviceEndpointRuntimeHealthEventSchema ToProtocol(this DeviceEndpointRuntimeHealthEventSchema source)
    {
        return new AdrBaseService.DeviceEndpointRuntimeHealthEventSchema
        {
            RuntimeHealth = source.RuntimeHealth.ToProtocol(),
        };
    }

    internal static AdrBaseService.StreamRuntimeHealthEventTelemetry ToProtocol(this StreamRuntimeHealthEventTelemetry source)
    {
        return new AdrBaseService.StreamRuntimeHealthEventTelemetry
        {
            StreamRuntimeHealthEvent = source.StreamRuntimeHealthEvent.ToProtocol()
        };
    }

    internal static AdrBaseService.ManagementActionRuntimeHealthEventTelemetry ToProtocol(this ManagementActionRuntimeHealthEventTelemetry source)
    {
        return new AdrBaseService.ManagementActionRuntimeHealthEventTelemetry
        {
            ManagementActionRuntimeHealthEvent = source.ManagementActionRuntimeHealthEvent.ToProtocol()
        };
    }

    internal static AdrBaseService.EventRuntimeHealthEventTelemetry ToProtocol(this EventRuntimeHealthEventTelemetry source)
    {
        return new AdrBaseService.EventRuntimeHealthEventTelemetry
        {
            EventRuntimeHealthEvent = source.EventRuntimeHealthEvent.ToProtocol()
        };
    }

    internal static AdrBaseService.DatasetRuntimeHealthEventTelemetry ToProtocol(this DatasetRuntimeHealthEventTelemetry source)
    {
        return new AdrBaseService.DatasetRuntimeHealthEventTelemetry
        {
            DatasetRuntimeHealthEvent = source.DatasetRuntimeHealthEvent.ToProtocol()
        };
    }

    internal static AdrBaseService.DeviceEndpointRuntimeHealthEventTelemetry ToProtocol(this DeviceEndpointRuntimeHealthEventTelemetry source)
    {
        return new AdrBaseService.DeviceEndpointRuntimeHealthEventTelemetry
        {
            DeviceEndpointRuntimeHealthEvent = source.DeviceEndpointRuntimeHealthEvent.ToProtocol()
        };
    }


    internal static AdrBaseService.RuntimeHealth ToProtocol(this RuntimeHealth source)
    {
        return new AdrBaseService.RuntimeHealth
        {
            LastUpdateTime = source.LastUpdateTime,
            Message = source.Message,
            ReasonCode = source.ReasonCode,
            Status = (AdrBaseService.StatusSchema) ((int) source.Status),
            Version = source.Version,
        };
    }
}
