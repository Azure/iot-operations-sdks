﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AepTypeService;

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

    internal static AdrBaseService.DiscoveredAsset ToProtocol(this DiscoveredAsset source)
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

    internal static DiscoveredAssetEvent ToProtocol(this DetectedAssetEventSchemaElement source)
    {
        return new DiscoveredAssetEvent
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

    internal static AdrBaseService.DiscoveredAssetEventDataPoint ToProtocol(this DiscoveredAssetEventDataPoint source)
    {
        return new AdrBaseService.DiscoveredAssetEventDataPoint
        {
            DataPointConfiguration = source.DataPointConfiguration?.RootElement.ToString(),
            DataSource = source.DataSource,
            LastUpdatedOn = source.LastUpdatedOn,
            Name = source.Name,
        };
    }
    internal static AdrBaseService.DiscoveredAssetDataset ToProtocol(this DiscoveredAssetDataset source)
    {
        return new AdrBaseService.DiscoveredAssetDataset
        {
            Name = source.Name,
            DatasetConfiguration = source.DataSetConfiguration?.RootElement.ToString(),
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
            DataPointConfiguration = source.DataPointConfiguration?.RootElement.ToString(),
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
            StreamConfiguration = source.StreamConfiguration?.RootElement.ToString(),
        };
    }
internal static AdrBaseService.DiscoveredAssetManagementGroup ToProtocol(this DiscoveredAssetManagementGroup source)
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

internal static AdrBaseService.DiscoveredAssetManagementGroupAction ToProtocol(this DiscoveredAssetManagementGroupAction source)
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

    public static CreateDiscoveredAssetEndpointProfileRequestPayload ToProtocol(this CreateDiscoveredAssetEndpointProfileRequest source)
    {
        return new CreateDiscoveredAssetEndpointProfileRequestPayload
        {
            DiscoveredAssetEndpointProfile = new DiscoveredAssetEndpointProfile
            {
                AdditionalConfiguration = source.AdditionalConfiguration,
                EndpointProfileType = source.EndpointProfileType,
                TargetAddress = source.TargetAddress,
                DaepName = source.Name,
                SupportedAuthenticationMethods = source.SupportedAuthenticationMethods?.Select(x => x.ToProtocol()).ToList()
            }
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

    internal static AdrBaseService.AssetStatus ToProtocol(this AssetStatus source)
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

    internal static AdrBaseService.Topic ToProtocol(this Topic source)
    {
        return new AdrBaseService.Topic
        {
            Path = source.Path,
            Retain = source.Retain?.ToProtocol()
        };
    }

    internal static AdrBaseService.Retain ToProtocol(this Retain source)
    {
        return (AdrBaseService.Retain)(int)source;
    }

    internal static SupportedAuthenticationMethodsSchemaElementSchema ToProtocol(this SupportedAuthenticationMethodsSchemaElement source)
    {
        return (SupportedAuthenticationMethodsSchemaElementSchema)(int)source;
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
            InnerError = source.InnerError
        };
    }

    internal static AdrBaseService.DetailsSchemaElementSchema ToProtocol(this DetailsSchemaElement source)
    {
        return new AdrBaseService.DetailsSchemaElementSchema
        {
            Code = source.Code,
            Message = source.Message,
            CorrelationId = source.CorrelationId,
            Info = source.Info
        };
    }
}
