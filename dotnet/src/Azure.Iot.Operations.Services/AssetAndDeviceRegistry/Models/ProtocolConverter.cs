// Copyright (c) Microsoft Corporation.
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

    public static CreateDetectedAssetRequestPayload ToProtocol(this CreateDetectedAssetRequest source)
    {
        return new CreateDetectedAssetRequestPayload
        {
            DetectedAsset = new DetectedAsset
            {
                AssetEndpointProfileRef = source.AssetEndpointProfileRef,
                AssetName = source.AssetName,
                Datasets = source.Datasets?.Select(x => x.ToProtocol()).ToList(),
                DefaultDatasetsConfiguration = source.DefaultDatasetsConfiguration,
                DefaultEventsConfiguration = source.DefaultEventsConfiguration,
                DefaultTopic = source.DefaultTopic?.ToProtocol(),
                DocumentationUri = source.DocumentationUri,
                Events = source.Events?.Select(x => x.ToProtocol()).ToList(),
                HardwareRevision = source.HardwareRevision,
                Manufacturer = source.Manufacturer,
                ManufacturerUri = source.ManufacturerUri,
                Model = source.Model,
                ProductCode = source.ProductCode,
                SerialNumber = source.SerialNumber,
                SoftwareRevision = source.SoftwareRevision
            }
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
            Endpoints = source.Endpoints?.ToProtocol()
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

    internal static AssetStatusManagementGroupSchemaElementSchema ToProtocol(this AssetStatusManagementGroupSchemaElement source)
    {
        return new AssetStatusManagementGroupSchemaElementSchema
        {
            Name = source.Name,
            Actions = source.Actions?.Select(x => x.ToProtocol()).ToList()
        };
    }

    internal static AssetStatusStreamSchemaElementSchema ToProtocol(this AssetStatusStreamSchemaElement source)
    {
        return new AssetStatusStreamSchemaElementSchema
        {
            Name = source.Name,
            MessageSchemaReference = source.MessageSchemaReference?.ToProtocol(),
            Error = source.Error?.ToProtocol()
        };
    }

    internal static AssetStatusManagementGroupActionSchemaElementSchema ToProtocol(this AssetStatusManagementGroupActionSchemaElement source)
    {
        return new AssetStatusManagementGroupActionSchemaElementSchema
        {
            Error = source.Error?.ToProtocol(),
            Name = source.Name,
            RequestMessageSchemaReference = source.RequestMessageSchemaReference?.ToProtocol(),
            ResponseMessageSchemaReference = source.ResponseMessageSchemaReference?.ToProtocol()
        };
    }

    internal static AssetStatusConfigSchema ToProtocol(this AssetStatusConfig source)
    {
        return new AssetStatusConfigSchema
        {
            Error = source.Error?.ToProtocol(),
            LastTransitionTime = source.LastTransitionTime,
            Version = source.Version
        };
    }

    internal static AssetStatusDatasetSchemaElementSchema ToProtocol(this AssetStatusDatasetSchemaElement source)
    {
        return new AssetStatusDatasetSchemaElementSchema
        {
            Name = source.Name,
            Error = source.Error?.ToProtocol(),
            MessageSchemaReference = source.MessageSchemaReference?.ToProtocol()
        };
    }

    internal static AssetStatusEventSchemaElementSchema ToProtocol(this EventsSchemaElement source)
    {
        return new AssetStatusEventSchemaElementSchema
        {
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

    internal static DetectedAssetDatasetSchemaElementSchema ToProtocol(this DetectedAssetDatasetSchemaElement source)
    {
        return new DetectedAssetDatasetSchemaElementSchema
        {
            Name = source.Name,
            DataSetConfiguration = source.DataSetConfiguration,
            DataPoints = source.DataPoints?.Select(x => x.ToProtocol()).ToList(),
            Topic = source.Topic?.ToProtocol()
        };
    }

    internal static DetectedAssetEventSchemaElementSchema ToProtocol(this DetectedAssetEventSchemaElement source)
    {
        return new DetectedAssetEventSchemaElementSchema
        {
            Name = source.Name,
            EventConfiguration = source.EventConfiguration,
            Topic = source.Topic?.ToProtocol()
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

    internal static DetectedAssetDataPointSchemaElementSchema ToProtocol(this DetectedAssetDataPointSchemaElement source)
    {
        return new DetectedAssetDataPointSchemaElementSchema
        {
            Name = source.Name,
            DataPointConfiguration = source.DataPointConfiguration,
            DataSource = source.DataSource,
            LastUpdatedOn = source.LastUpdatedOn
        };
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
