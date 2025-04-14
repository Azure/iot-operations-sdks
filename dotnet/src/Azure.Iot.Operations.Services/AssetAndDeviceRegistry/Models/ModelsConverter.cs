// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AepTypeService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

internal static class ModelsConverter
{
    public static AssetStatus ToModel(this AdrBaseService.AssetStatus source)
    {
        return new AssetStatus
        {
            Errors = source.Errors?.Select(x => x.ToModel()).ToList(),
            DatasetsSchema = source.DatasetsSchema?.Select(x => x.ToModel()).ToList(),
            EventsSchema = source.EventsSchema?.Select(x => x.ToModel()).ToList()
        };
    }

    public static DatasetsSchemaElement ToModel(this DatasetsSchemaSchemaElementSchema source)
    {
        return new DatasetsSchemaElement
        {
            Name = source.Name,
            MessageSchemaReference = source.MessageSchemaReference?.ToModel()
        };
    }

    public static EventsSchemaElement ToModel(this EventsSchemaSchemaElementSchema source)
    {
        return new EventsSchemaElement
        {
            Name = source.Name,
            MessageSchemaReference = source.MessageSchemaReference?.ToModel()
        };
    }

    public static Asset ToModel(this AdrBaseService.Asset source)
    {
        return new Asset
        {
            Name = source.Name,
            Specification = source.Specification?.ToModel(),
            Status = source.Status?.ToModel()
        };
    }

    public static MessageSchemaReference ToModel(this AdrBaseService.MessageSchemaReference source)
    {
        return new MessageSchemaReference
        {
            SchemaName = source.SchemaName,
            SchemaNamespace = source.SchemaNamespace,
            SchemaVersion = source.SchemaVersion
        };
    }

    public static AssetSpecification ToModel(this AssetSpecificationSchema source)
    {
        return new AssetSpecification
        {
            Datasets = source.Datasets?.Select(x => x.ToModel()).ToList(),
            Attributes = source.Attributes ?? new Dictionary<string, string>(),
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

    public static AssetDatasetSchemaElement ToModel(this AssetDatasetSchemaElementSchema source)
    {
        return new AssetDatasetSchemaElement
        {
            Name = source.Name,
            Topic = source.Topic?.ToModel(),
            DataPoints = source.DataPoints?.Select(x => x.ToModel()).ToList(),
            DatasetConfiguration = source.DatasetConfiguration
        };
    }

    public static AssetEventSchemaElement ToModel(this AssetEventSchemaElementSchema source)
    {
        return new AssetEventSchemaElement
        {
            Name = source.Name,
            Topic = source.Topic?.ToModel(),
            EventConfiguration = source.EventConfiguration,
            EventNotifier = source.EventNotifier,
            ObservabilityMode = source.ObservabilityMode?.ToModel()
        };
    }

    public static AssetEventObservabilityMode ToModel(this AssetEventObservabilityModeSchema source)
    {
        return (AssetEventObservabilityMode)(int)source;
    }

    public static Topic ToModel(this AdrBaseService.Topic source)
    {
        return new Topic
        {
            Path = source.Path,
            Retain = source.Retain?.ToModel()
        };
    }

    public static Retain ToModel(this RetainSchema source)
    {
        return (Retain)(int)source;
    }

    public static AssetDataPointSchemaElement ToModel(this AssetDataPointSchemaElementSchema source)
    {
        return new AssetDataPointSchemaElement
        {
            DataPointConfiguration = source.DataPointConfiguration,
            DataSource = source.DataSource,
            Name = source.Name,
            ObservabilityMode = source.ObservabilityMode?.ToModel()
        };
    }

    public static AssetDataPointObservabilityMode ToModel(this AssetDataPointObservabilityModeSchema source)
    {
        return (AssetDataPointObservabilityMode)(int)source;
    }

    public static DetectedAssetDataPointSchemaElement ToModel(this DetectedAssetDataPointSchemaElementSchema source)
    {
        return new DetectedAssetDataPointSchemaElement
        {
            DataPointConfiguration = source.DataPointConfiguration,
            DataSource = source.DataSource,
            Name = source.Name,
            LastUpdatedOn = source.DataSource
        };
    }

    public static NotificationResponse ToModel(this AdrBaseService.NotificationResponse source)
    {
        return (NotificationResponse)(int)source;
    }

    public static AssetEndpointProfile ToModel(this AdrBaseService.AssetEndpointProfile source)
    {
        return new AssetEndpointProfile
        {
            Name = source.Name,
            Specification = source.Specification?.ToModel(),
            Status = source.Status?.ToModel()
        };
    }

    public static AssetEndpointProfileSpecification ToModel(this AssetEndpointProfileSpecificationSchema source)
    {
        return new AssetEndpointProfileSpecification
        {
            Uuid = source.Uuid,
            Authentication = source.Authentication?.ToModel(),
            AdditionalConfiguration = source.AdditionalConfiguration,
            TargetAddress = source.TargetAddress,
            EndpointProfileType = source.EndpointProfileType,
            DiscoveredAssetEndpointProfileRef = source.DiscoveredAssetEndpointProfileRef
        };
    }

    public static Authentication ToModel(this AuthenticationSchema source)
    {
        return new Authentication
        {
            Method = source.Method?.ToModel(),
            X509Credentials = source.X509credentials?.ToModel(),
            UsernamePasswordCredentials = source.UsernamePasswordCredentials?.ToModel()
        };
    }

    public static Method ToModel(this MethodSchema source)
    {
        return (Method)(int)source;
    }

    public static X509Credentials ToModel(this X509credentialsSchema source)
    {
        return new X509Credentials
        {
            CertificateSecretName = source.CertificateSecretName
        };
    }

    public static UsernamePasswordCredentials ToModel(this UsernamePasswordCredentialsSchema source)
    {
        return new UsernamePasswordCredentials
        {
            PasswordSecretName = source.PasswordSecretName,
            UsernameSecretName = source.UsernameSecretName
        };
    }

    public static AssetEndpointProfileStatus ToModel(this AdrBaseService.AssetEndpointProfileStatus source)
    {
        return new AssetEndpointProfileStatus
        {
            Errors = source.Errors?.Select(x => x.ToModel()).ToList()
        };
    }

    public static Error ToModel(this AdrBaseService.Error source)
    {
        return new Error
        {
            Code = source.Code,
            Message = source.Message
        };
    }

    public static CreateDetectedAssetResponse ToModel(this CreateDetectedAssetResponseSchema source)
    {
        if (source.Status != null)
            return new CreateDetectedAssetResponse
            {
                Status = (DetectedAssetResponseStatus)(int)source.Status
            };
        return new CreateDetectedAssetResponse();
    }

    public static CreateDiscoveredAssetEndpointProfileResponse ToModel(this CreateDiscoveredAssetEndpointProfileResponseSchema source)
    {
        if (source.Status != null)
            return new CreateDiscoveredAssetEndpointProfileResponse
            {
                Status = (DiscoveredAssetEndpointProfileResponseStatus)(int)source.Status
            };
        return new CreateDiscoveredAssetEndpointProfileResponse();
    }
}
