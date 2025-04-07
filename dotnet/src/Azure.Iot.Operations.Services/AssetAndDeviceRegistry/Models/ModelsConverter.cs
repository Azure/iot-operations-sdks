// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public static class ModelsConverter
{
    public static Models.AssetStatus ToModel(this AdrBaseService.AssetStatus source){
        return new Models.AssetStatus
        {
            Errors = source.Errors?.Select(x => x.ToModel()).ToList(),
            DatasetsSchema = source.DatasetsSchema?.Select(x => x.ToModel()).ToList(),
            EventsSchema = source.EventsSchema?.Select(x => x.ToModel()).ToList(),
        };
    }

    public static Models.DatasetsSchemaSchemaElementSchema ToModel(this AdrBaseService.DatasetsSchemaSchemaElementSchema source)
    {
        return new Models.DatasetsSchemaSchemaElementSchema
        {
            Name = source.Name,
            MessageSchemaReference = source.MessageSchemaReference?.ToModel()
        };
    }

    public static Models.EventsSchemaSchemaElementSchema ToModel(this AdrBaseService.EventsSchemaSchemaElementSchema source)
    {
        return new Models.EventsSchemaSchemaElementSchema
        {
            Name = source.Name,
            MessageSchemaReference = source.MessageSchemaReference?.ToModel()
        };
    }

    public static Models.AssetResponse ToModel(this AdrBaseService.Asset source)
    {
        return new Models.AssetResponse
        {
            Name = source.Name,
            Specification = source.Specification?.ToModel(),
            Status = source.Status?.ToModel()
        };
    }

    public static Models.MessageSchemaReference ToModel(this AdrBaseService.MessageSchemaReference source)
    {
        return new Models.MessageSchemaReference
        {
            SchemaName = source.SchemaName,
            SchemaNamespace = source.SchemaNamespace,
            SchemaVersion = source.SchemaVersion
        };
    }
    public static Models.AssetSpecificationSchema ToModel(this AdrBaseService.AssetSpecificationSchema source)
    {
        return new Models.AssetSpecificationSchema
        {
            Datasets = source.Datasets?.Select(x => x.ToModel()).ToList(),
            Attributes = source.Attributes?? new Dictionary<string, string>(),
            Description = source.Description,
            Enabled = source.Enabled,
            Events = source.Events?.Select(x => x.ToModel()).ToList(),
            Manufacturer = source.Manufacturer,
            Model = source.Model,
            Uuid = source.Uuid,
            Version = source.Version,
            DefaultTopic = source.DefaultTopic?.ToModel(),
            DisplayName = source.DisplayName,
            DocumentationUri = source.DocumentationUri,
            HardwareRevision = source.HardwareRevision,
            ManufacturerUri = source.ManufacturerUri,
            ProductCode = source.ProductCode,
            SerialNumber = source.SerialNumber,
            SoftwareRevision = source.SoftwareRevision,
            DefaultDatasetsConfiguration = source.DefaultDatasetsConfiguration,
            DefaultEventsConfiguration = source.DefaultEventsConfiguration,
            DiscoveredAssetRefs = source.DiscoveredAssetRefs,
            ExternalAssetId = source.ExternalAssetId,
            AssetEndpointProfileRef = source.AssetEndpointProfileRef
        };
    }

    public static Models.AssetDatasetSchemaElementSchema ToModel(this AdrBaseService.AssetDatasetSchemaElementSchema source)
    {
        return new Models.AssetDatasetSchemaElementSchema
        {
            Name = source.Name,
            Topic = source.Topic?.ToModel(),
            DataPoints = source.DataPoints?.Select(x => x.ToModel()).ToList(),
            DatasetConfiguration = source.DatasetConfiguration
        };
    }

    public static Models.AssetEventSchemaElementSchema ToModel(this AdrBaseService.AssetEventSchemaElementSchema source)
    {
        return new Models.AssetEventSchemaElementSchema
        {
            Name = source.Name,
            Topic = source.Topic?.ToModel(),
            EventConfiguration = source.EventConfiguration,
            EventNotifier = source.EventNotifier,
            ObservabilityMode = source.ObservabilityMode?.ToModel()
        };
    }

    public static Models.AssetEventObservabilityModeSchema ToModel(this AdrBaseService.AssetEventObservabilityModeSchema source)
    {
        return (Models.AssetEventObservabilityModeSchema)(int)source;
    }

    public static Models.Topic ToModel(this AdrBaseService.Topic source)
    {
        return new Models.Topic
        {
            Path = source.Path,
            Retain = source.Retain?.ToModel()
        };
    }

    public static Models.RetainSchema ToModel(this AdrBaseService.RetainSchema source)
    {
        return (Models.RetainSchema)(int)source;
    }

    public static Models.DetectedAssetDatasetSchemaElementSchema ToModel(this AdrBaseService.DetectedAssetDatasetSchemaElementSchema source)
    {
        return new Models.DetectedAssetDatasetSchemaElementSchema
        {
            Name = source.Name,
            Topic = source.Topic?.ToModel(),
            DataPoints = source.DataPoints?.Select(x => x.ToModel()).ToList(),
            DataSetConfiguration = source.DataSetConfiguration
        };
    }

    public static Models.DetectedAssetEventSchemaElementSchema ToModel(this AdrBaseService.DetectedAssetEventSchemaElementSchema source)
    {
        return new Models.DetectedAssetEventSchemaElementSchema
        {
            Name = source.Name,
            Topic = source.Topic?.ToModel(),
            EventConfiguration = source.EventConfiguration,
            EventNotifier = source.EventNotifier,
            LastUpdatedOn = source.LastUpdatedOn
        };
    }

    public static Models.AssetDataPointSchemaElementSchema ToModel(this AdrBaseService.AssetDataPointSchemaElementSchema source)
    {
        return new Models.AssetDataPointSchemaElementSchema
        {
            DataPointConfiguration = source.DataPointConfiguration,
            DataSource = source.DataSource,
            Name = source.Name,
            ObservabilityMode = source.ObservabilityMode?.ToModel(),
        };
    }

    public static Models.AssetDataPointObservabilityModeSchema ToModel(this AdrBaseService.AssetDataPointObservabilityModeSchema source)
    {
        return (Models.AssetDataPointObservabilityModeSchema)(int)source;
    }

    public static Models.DetectedAssetDataPointSchemaElementSchema ToModel(this AdrBaseService.DetectedAssetDataPointSchemaElementSchema source)
    {
        return new Models.DetectedAssetDataPointSchemaElementSchema
        {
            DataPointConfiguration = source.DataPointConfiguration,
            DataSource = source.DataSource,
            Name = source.Name,
            LastUpdatedOn = source.DataSource
        };
    }

    public static Models.NotificationResponse ToModel(this AdrBaseService.NotificationResponse source)
    {
        return (Models.NotificationResponse)(int)source;
    }

    public static Models.AssetEndpointProfileResponse ToModel(this AdrBaseService.AssetEndpointProfile source)
    {
        return new AssetEndpointProfileResponse
        {
            Name = source.Name,
            Specification = source.Specification?.ToModel(),
            Status = source.Status?.ToModel(),
        };
    }

    public static Models.AssetEndpointProfileSpecificationSchema ToModel(this AdrBaseService.AssetEndpointProfileSpecificationSchema source)
    {
        return new Models.AssetEndpointProfileSpecificationSchema
        {
            Uuid = source.Uuid,
            Authentication = source.Authentication?.ToModel(),
            AdditionalConfiguration = source.AdditionalConfiguration,
            TargetAddress = source.TargetAddress,
            EndpointProfileType = source.EndpointProfileType,
            DiscoveredAssetEndpointProfileRef = source.DiscoveredAssetEndpointProfileRef,
        };
    }

    public static Models.AuthenticationSchema ToModel(this AdrBaseService.AuthenticationSchema source)
    {
        return new Models.AuthenticationSchema
        {
            Method = source.Method?.ToModel(),
            X509credentials = source.X509credentials?.ToModel(),
            UsernamePasswordCredentials = source.UsernamePasswordCredentials?.ToModel(),
        };
    }

    public static Models.MethodSchema ToModel(this AdrBaseService.MethodSchema source)
    {
        return (Models.MethodSchema)(int)source;
    }

    public static Models.X509credentialsSchema ToModel(this AdrBaseService.X509credentialsSchema source)
    {
        return new Models.X509credentialsSchema
        {
            CertificateSecretName = source.CertificateSecretName
        };
    }

    public static Models.UsernamePasswordCredentialsSchema ToModel(this AdrBaseService.UsernamePasswordCredentialsSchema source)
    {
        return new Models.UsernamePasswordCredentialsSchema
        {
            PasswordSecretName = source.PasswordSecretName,
            UsernameSecretName = source.UsernameSecretName
        };
    }

    public static Models.AssetEndpointProfileStatus ToModel(this AdrBaseService.AssetEndpointProfileStatus source)
    {
        return new Models.AssetEndpointProfileStatus
        {
            Errors = source.Errors?.Select(x => x.ToModel()).ToList()
        };
    }

    public static Models.Error ToModel(this AdrBaseService.Error source)
    {
        return new Models.Error
        {
            Code = source.Code,
            Message = source.Message,
        };
    }

    public static Models.CreateDetectedAssetResponse ToModel(this AdrBaseService.CreateDetectedAssetResponseSchema source)
    {
        if (source.Status != null)
            return new Models.CreateDetectedAssetResponse
            {
                Status = (Models.DetectedAssetResponseStatusSchema)(int)source.Status
            };
        return new Models.CreateDetectedAssetResponse();
    }

    public static Models.CreateDiscoveredAssetEndpointProfileResponse ToModel(this AepTypeService.CreateDiscoveredAssetEndpointProfileResponseSchema source)
    {
        if (source.Status != null)
            return new Models.CreateDiscoveredAssetEndpointProfileResponse
            {
                Status = (Models.DiscoveredAssetEndpointProfileResponseStatusSchema)(int)source.Status
            };
        return new Models.CreateDiscoveredAssetEndpointProfileResponse();
    }
}
