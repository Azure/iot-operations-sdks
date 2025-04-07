// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AepTypeService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public static class ProtocolConverter
{
    public static GetAssetRequestPayload ToProtocol(this GetAssetRequest source)
    {
        return new GetAssetRequestPayload
        {
            AssetName = source.AssetName,
        };
    }

    public static UpdateAssetStatusRequestPayload ToProtocol(this UpdateAssetStatusRequest source)
    {
        return new UpdateAssetStatusRequestPayload
        {
            AssetStatusUpdate = new UpdateAssetStatusRequestSchema
            {
                AssetName = source.AssetName,
                AssetStatus = source.AssetStatus?.ToProtocol(),
            },
        };
    }

    public static AdrBaseService.AssetStatus ToProtocol(this AssetStatus source)
    {
        return new AdrBaseService.AssetStatus
        {
            Errors = source.Errors?.Select(x => x.ToProtocol()).ToList(),
            DatasetsSchema = source.DatasetsSchema?.Select(x => x.ToProtocol()).ToList(),
            EventsSchema = source.EventsSchema?.Select(x => x.ToProtocol()).ToList(),
            Version = source.Version
        };
    }

    public static AdrBaseService.Error ToProtocol(this Error source)
    {
        return new AdrBaseService.Error
        {
            Code = source.Code,
            Message = source.Message
        };
    }

    public static AdrBaseService.DatasetsSchemaSchemaElementSchema ToProtocol(this DatasetsSchemaSchemaElement source)
    {
        return new AdrBaseService.DatasetsSchemaSchemaElementSchema
        {
            Name = source.Name,
            MessageSchemaReference = source.MessageSchemaReference?.ToProtocol()
        };
    }

    public static AdrBaseService.EventsSchemaSchemaElementSchema ToProtocol(this EventsSchemaSchemaElement source)
    {
        return new AdrBaseService.EventsSchemaSchemaElementSchema
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
            SchemaNamespace = source.SchemaNamespace,
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

    public static AdrBaseService.AssetDatasetSchemaElementSchema ToProtocol(this AssetDatasetSchemaElement source)
    {
        return new AdrBaseService.AssetDatasetSchemaElementSchema
        {
            Name = source.Name,
            DatasetConfiguration = source.DatasetConfiguration,
            DataPoints = source.DataPoints?.Select(x => x.ToProtocol()).ToList(),
            Topic = source.Topic?.ToProtocol()
        };
    }

    public static AdrBaseService.DetectedAssetDatasetSchemaElementSchema ToProtocol(this DetectedAssetDatasetSchemaElement source)
    {
        return new AdrBaseService.DetectedAssetDatasetSchemaElementSchema
        {
            Name = source.Name,
            DataSetConfiguration = source.DataSetConfiguration,
            DataPoints = source.DataPoints?.Select(x => x.ToProtocol()).ToList(),
            Topic = source.Topic?.ToProtocol()
        };
    }

    public static AdrBaseService.DetectedAssetEventSchemaElementSchema ToProtocol(this DetectedAssetEventSchemaElement source)
    {
        return new AdrBaseService.DetectedAssetEventSchemaElementSchema
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
            Retain = source.Retain?.ToProtocol(),
        };
    }

    public static AdrBaseService.RetainSchema ToProtocol(this Retain source)
    {
        return (AdrBaseService.RetainSchema)(int)source;
    }

    public static AdrBaseService.AssetDataPointSchemaElementSchema ToProtocol(this AssetDataPointSchemaElement source)
    {
        return new AdrBaseService.AssetDataPointSchemaElementSchema
        {
            Name = source.Name,
            DataPointConfiguration = source.DataPointConfiguration,
            DataSource = source.DataSource,
            ObservabilityMode = source.ObservabilityMode?.ToProtocol(),
        };
    }

    public static AdrBaseService.DetectedAssetDataPointSchemaElementSchema ToProtocol(this DetectedAssetDataPointSchemaElement source)
    {
        return new AdrBaseService.DetectedAssetDataPointSchemaElementSchema
        {
            Name = source.Name,
            DataPointConfiguration = source.DataPointConfiguration,
            DataSource = source.DataSource,
            LastUpdatedOn = source.LastUpdatedOn
        };
    }

    public static AdrBaseService.AssetDataPointObservabilityModeSchema ToProtocol(this AssetDataPointObservabilityMode source)
    {
        return (AdrBaseService.AssetDataPointObservabilityModeSchema)(int)source;
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
                DaepName = source.DaepName,
                SupportedAuthenticationMethods = source.SupportedAuthenticationMethods?.Select(x => x.ToProtocol()).ToList()
            }
        };
    }

    public static AepTypeService.SupportedAuthenticationMethodsSchemaElementSchema ToProtocol(this SupportedAuthenticationMethodsSchemaElement source)
    {
        return (AepTypeService.SupportedAuthenticationMethodsSchemaElementSchema)(int)source;
    }

    public static UpdateAssetEndpointProfileStatusRequestPayload ToProtocol(this UpdateAssetEndpointProfileStatusRequest source)
    {
        return new UpdateAssetEndpointProfileStatusRequestPayload
        {
            AssetEndpointProfileStatusUpdate = new AdrBaseService.AssetEndpointProfileStatus
            {
                Errors = source.Errors?.Select(x => x.ToProtocol()).ToList(),
            },
        };
    }
}
