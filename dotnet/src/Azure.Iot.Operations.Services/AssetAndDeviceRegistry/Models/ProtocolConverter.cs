// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AepTypeService;
using CreateDiscoveredAssetEndpointProfileRequestPayload =
    Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AepTypeService.CreateDiscoveredAssetEndpointProfileRequestPayload;
using SupportedAuthenticationMethodsSchemaElementSchema =
    Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AepTypeService.SupportedAuthenticationMethodsSchemaElementSchema;

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

    public static AdrBaseService.AssetStatus ToProtocol(this AssetStatus source)
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

    public static AssetStatusManagementGroupSchemaElementSchema ToProtocol(this AssetStatusManagementGroupSchemaElement source)
    {
        return new AssetStatusManagementGroupSchemaElementSchema
        {
            Name = source.Name,
            Actions = source.Actions?.Select(x => x.ToProtocol()).ToList()
        };
    }

    public static AssetStatusStreamSchemaElementSchema ToProtocol(this AssetStatusStreamSchemaElement source)
    {
        return new AssetStatusStreamSchemaElementSchema
        {
            Name = source.Name,
            MessageSchemaReference = source.MessageSchemaReference?.ToProtocol(),
            Error = source.Error?.ToProtocol()
        };
    }

    public static AssetStatusManagementGroupActionSchemaElementSchema ToProtocol(this AssetStatusManagementGroupActionSchemaElement source)
    {
        return new AssetStatusManagementGroupActionSchemaElementSchema
        {
            Error = source.Error?.ToProtocol(),
            Name = source.Name,
            RequestMessageSchemaReference = source.RequestMessageSchemaReference?.ToProtocol(),
            ResponseMessageSchemaReference = source.ResponseMessageSchemaReference?.ToProtocol()
        };
    }

    public static AssetStatusConfigSchema ToProtocol(this AssetStatusConfig source)
    {
        return new AssetStatusConfigSchema
        {
            Error = source.Error?.ToProtocol(),
            LastTransitionTime = source.LastTransitionTime,
            Version = source.Version
        };
    }

    public static AssetStatusDatasetSchemaElementSchema ToProtocol(this AssetStatusDatasetSchemaElement source)
    {
        return new AssetStatusDatasetSchemaElementSchema
        {
            Name = source.Name,
            Error = source.Error?.ToProtocol(),
            MessageSchemaReference = source.MessageSchemaReference?.ToProtocol()
        };
    }

    public static AssetStatusEventSchemaElementSchema ToProtocol(this EventsSchemaElement source)
    {
        return new AssetStatusEventSchemaElementSchema
        {
            Name = source.Name,
            MessageSchemaReference = source.MessageSchemaReference?.ToProtocol()
        };
    }

    public static AdrBaseService.MessageSchemaReference ToProtocol(this MessageSchemaReference source)
    {
        return new AdrBaseService.MessageSchemaReference
        {
            SchemaName = source.SchemaName,
            SchemaRegistryNamespace = source.SchemaRegistryNamespace,
            SchemaVersion = source.SchemaVersion
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

    public static DetectedAssetDatasetSchemaElementSchema ToProtocol(this DetectedAssetDatasetSchemaElement source)
    {
        return new DetectedAssetDatasetSchemaElementSchema
        {
            Name = source.Name,
            DataSetConfiguration = source.DataSetConfiguration,
            DataPoints = source.DataPoints?.Select(x => x.ToProtocol()).ToList(),
            Topic = source.Topic?.ToProtocol()
        };
    }

    public static DetectedAssetEventSchemaElementSchema ToProtocol(this DetectedAssetEventSchemaElement source)
    {
        return new DetectedAssetEventSchemaElementSchema
        {
            Name = source.Name,
            EventConfiguration = source.EventConfiguration,
            Topic = source.Topic?.ToProtocol()
        };
    }

    public static AdrBaseService.Topic ToProtocol(this Topic source)
    {
        return new AdrBaseService.Topic
        {
            Path = source.Path,
            Retain = source.Retain?.ToProtocol()
        };
    }

    public static AdrBaseService.Retain ToProtocol(this Retain source)
    {
        return (AdrBaseService.Retain)(int)source;
    }

    public static DetectedAssetDataPointSchemaElementSchema ToProtocol(this DetectedAssetDataPointSchemaElement source)
    {
        return new DetectedAssetDataPointSchemaElementSchema
        {
            Name = source.Name,
            DataPointConfiguration = source.DataPointConfiguration,
            DataSource = source.DataSource,
            LastUpdatedOn = source.LastUpdatedOn
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

    public static SupportedAuthenticationMethodsSchemaElementSchema ToProtocol(this SupportedAuthenticationMethodsSchemaElement source)
    {
        return (SupportedAuthenticationMethodsSchemaElementSchema)(int)source;
    }

    public static AdrBaseService.DeviceStatus ToProtocol(this DeviceStatus source)
    {
        return new AdrBaseService.DeviceStatus
        {
            Config = source.Config?.ToProtocol(),
            Endpoints = source.Endpoints?.ToProtocol()
        };
    }

    public static DeviceStatusConfigSchema ToProtocol(this DeviceStatusConfig source)
    {
        return new DeviceStatusConfigSchema
        {
            Error = source.Error?.ToProtocol(),
            LastTransitionTime = source.LastTransitionTime,
            Version = source.Version
        };
    }

    public static DeviceStatusEndpointSchema ToProtocol(this DeviceStatusEndpoint source)
    {
        return new DeviceStatusEndpointSchema
        {
            Inbound = new Dictionary<string, DeviceStatusInboundEndpointSchemaMapValueSchema>(
                source.Inbound?.Select(x => new KeyValuePair<string, DeviceStatusInboundEndpointSchemaMapValueSchema>(x.Key, x.Value.ToProtocol())) ??
                new Dictionary<string, DeviceStatusInboundEndpointSchemaMapValueSchema>())
        };
    }

    public static DeviceStatusInboundEndpointSchemaMapValueSchema ToProtocol(this DeviceStatusInboundEndpointSchemaMapValue source)
    {
        return new DeviceStatusInboundEndpointSchemaMapValueSchema
        {
            Error = source.Error?.ToProtocol()
        };
    }

    public static AdrBaseService.ConfigError ToProtocol(this ConfigError source)
    {
        return new AdrBaseService.ConfigError
        {
            Code = source.Code,
            Message = source.Message
        };
    }
}
