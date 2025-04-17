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
            Config = source.Config?.ToModel(),
            Datasets = source.Datasets?.Select(x => x.ToModel()).ToList(),
            Events = source.Events?.Select(x => x.ToModel()).ToList(),
            ManagementGroups = source.ManagementGroups?.Select(x => x.ToModel()).ToList(),
            Streams = source.Streams?.Select(x => x.ToModel()).ToList()
        };
    }

    public static Asset ToModel(this AdrBaseService.Asset source)
    {
        return new Asset
        {
            Name = source.Name,
            Specification = source.Specification.ToModel(),
            Status = source.Status?.ToModel()
        };
    }

    public static CreateDetectedAssetResponse ToModel(this CreateDetectedAssetResponseSchema source)
    {
        return new CreateDetectedAssetResponse
        {
            Status = (DetectedAssetResponseStatus)(int)source.Status
        };
    }

    public static CreateDiscoveredAssetEndpointProfileResponse ToModel(this CreateDiscoveredAssetEndpointProfileResponseSchema source)
    {
        return new CreateDiscoveredAssetEndpointProfileResponse
        {
            Status = (DiscoveredAssetEndpointProfileResponseStatus)(int)source.Status
        };
    }

    public static Device ToModel(this AdrBaseService.Device source)
    {
        return new Device
        {
            Name = source.Name,
            Specification = source.Specification.ToModel(),
            Status = source.Status?.ToModel()
        };
    }

    public static NotificationResponse ToModel(this NotificationPreferenceResponse source)
    {
        return (NotificationResponse)(int)source;
    }

    internal static MessageSchemaReference ToModel(this AdrBaseService.MessageSchemaReference source)
    {
        return new MessageSchemaReference
        {
            SchemaName = source.SchemaName,
            SchemaRegistryNamespace = source.SchemaRegistryNamespace,
            SchemaVersion = source.SchemaVersion
        };
    }

    internal static AssetSpecification ToModel(this AssetSpecificationSchema source)
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
            DefaultDatasetsDestinations = source.DefaultDatasetsDestinations?.Select(x => x.ToModel()).ToList(),
            DeviceRef = source.DeviceRef.ToModel(),
        };
    }

    internal static DefaultDatasetsDestinationsSchemaElement ToModel(this DefaultDatasetsDestinationsSchemaElementSchema source)
    {
        return new DefaultDatasetsDestinationsSchemaElement
        {
            Target = source.Target.ToModel(),
            Configuration = source.Configuration.ToModel(),
        };
    }

    internal static DatasetTarget ToModel(this AdrBaseService.DatasetTarget source)
    {
        return (DatasetTarget)(int)source;
    }

    internal static DestinationConfiguration ToModel(this AdrBaseService.DestinationConfiguration source)
    {
        return new DestinationConfiguration
        {
            Key = source.Key,
            Path = source.Path,
            Topic = source.Topic,
            Qos = source.Qos?.ToModel(),
            Retain = source.Retain?.ToModel(),
            Ttl = source.Ttl
        };
    }

    internal static Retain ToModel(this AdrBaseService.Retain source)
    {
        return (Retain)(int)source;
    }

    internal static QoS ToModel(this AdrBaseService.QoS source)
    {
        return (QoS)(int)source;
    }

    internal static AssetDatasetSchemaElement ToModel(this AssetDatasetSchemaElementSchema source)
    {
        return new AssetDatasetSchemaElement
        {
            Name = source.Name,
            DataPoints = source.DataPoints?.Select(x => x.ToModel()).ToList(),
            DataSource = source.DataSource,
            TypeRef = source.TypeRef,
            Destinations = source.Destinations?.Select(x => x.ToModel()).ToList(),
        };
    }

    internal static AssetDatasetDataPointSchemaElement ToModel(this AssetDatasetDataPointSchemaElementSchema source)
    {
        return new AssetDatasetDataPointSchemaElement
        {
            Name = source.Name,
            DataSource = source.DataSource,
            DataPointConfiguration = source.DataPointConfiguration,
            TypeRef = source.TypeRef,
        };
    }

    internal static AssetDatasetDestinationSchemaElement ToModel(this AssetDatasetDestinationSchemaElementSchema source)
    {
        return new AssetDatasetDestinationSchemaElement
        {
            Configuration = source.Configuration.ToModel(),
            Target = source.Target.ToModel(),
        };
    }

    internal static AssetEventSchemaElement ToModel(this AssetEventSchemaElementSchema source)
    {
        return new AssetEventSchemaElement
        {
            Name = source.Name,
            Destinations = source.Destinations?.Select(x => x.ToModel()).ToList(),
            EventConfiguration = source.EventConfiguration,
            EventNotifier = source.EventNotifier,
            DataPoints = source.DataPoints?.Select(x => x.ToModel()).ToList(),
        };
    }

    internal static AssetEventDestinationSchemaElement ToModel(this AssetEventDestinationSchemaElementSchema source)
    {
        return new AssetEventDestinationSchemaElement
        {
            Configuration = source.Configuration.ToModel(),
            Target = source.Target.ToModel(),
        };
    }

    internal static EventStreamTarget ToModel(this AdrBaseService.EventStreamTarget source)
    {
        return (EventStreamTarget)(int)source;
    }

    internal static AssetEventDataPointSchemaElement ToModel(this AssetEventDataPointSchemaElementSchema source)
    {
        return new AssetEventDataPointSchemaElement
        {
            DataSource = source.DataSource,
            DataPointConfiguration = source.DataPointConfiguration,
            Name = source.Name
        };
    }

    internal static Authentication ToModel(this AuthenticationSchema source)
    {
        return new Authentication
        {
            Method = source.Method.ToModel(),
            X509Credentials = source.X509credentials?.ToModel(),
            UsernamePasswordCredentials = source.UsernamePasswordCredentials?.ToModel()
        };
    }

    internal static Method ToModel(this MethodSchema source)
    {
        return (Method)(int)source;
    }

    internal static X509Credentials ToModel(this X509credentialsSchema source)
    {
        return new X509Credentials
        {
            CertificateSecretName = source.CertificateSecretName
        };
    }

    internal static UsernamePasswordCredentials ToModel(this UsernamePasswordCredentialsSchema source)
    {
        return new UsernamePasswordCredentials
        {
            PasswordSecretName = source.PasswordSecretName,
            UsernameSecretName = source.UsernameSecretName
        };
    }

    internal static DeviceSpecification ToModel(this DeviceSpecificationSchema source)
    {
        return new DeviceSpecification
        {
            Attributes = source.Attributes ?? new Dictionary<string, string>(),
            Enabled = source.Enabled,
            Manufacturer = source.Manufacturer,
            Model = source.Model,
            Uuid = source.Uuid,
            Version = source.Version,
            DiscoveredDeviceRef = source.DiscoveredDeviceRef,
            Endpoints = source.Endpoints?.ToModel(),
            ExternalDeviceId = source.ExternalDeviceId,
            LastTransitionTime = source.LastTransitionTime,
            OperatingSystemVersion = source.OperatingSystemVersion
        };
    }

    internal static DeviceEndpoint ToModel(this DeviceEndpointSchema source)
    {
        return new DeviceEndpoint
        {
            Inbound = new Dictionary<string, DeviceInboundEndpointSchemaMapValue>(
                source.Inbound?.Select(x => new KeyValuePair<string, DeviceInboundEndpointSchemaMapValue>(x.Key, x.Value.ToModel())) ??
                new Dictionary<string, DeviceInboundEndpointSchemaMapValue>())
        };
    }

    internal static DeviceInboundEndpointSchemaMapValue ToModel(this DeviceInboundEndpointSchemaMapValueSchema source)
    {
        return new DeviceInboundEndpointSchemaMapValue
        {
            Address = source.Address,
            AdditionalConfiguration = source.AdditionalConfiguration,
            Version = source.Version,
            Type = source.Type,
            Authentication = source.Authentication?.ToModel(),
            TrustSettings = source.TrustSettings?.ToModel()
        };
    }

    internal static TrustSettings ToModel(this TrustSettingsSchema source)
    {
        return new TrustSettings
        {
            IssuerList = source.IssuerList,
            TrustList = source.TrustList,
            TrustMode = source.TrustMode
        };
    }

    internal static DeviceStatus ToModel(this AdrBaseService.DeviceStatus source)
    {
        return new DeviceStatus
        {
            Endpoints = source.Endpoints?.ToModel(),
            Config = source.Config?.ToModel()
        };
    }

    internal static DeviceStatusConfig ToModel(this DeviceStatusConfigSchema source)
    {
        return new DeviceStatusConfig
        {
            Error = source.Error?.ToModel(),
            Version = source.Version,
            LastTransitionTime = source.LastTransitionTime
        };
    }

    internal static DeviceStatusEndpoint ToModel(this DeviceStatusEndpointSchema source)
    {
        return new DeviceStatusEndpoint
        {
            Inbound = new Dictionary<string, DeviceStatusInboundEndpointSchemaMapValue>(
                source.Inbound?.Select(x => new KeyValuePair<string, DeviceStatusInboundEndpointSchemaMapValue>(x.Key, x.Value.ToModel())) ??
                new Dictionary<string, DeviceStatusInboundEndpointSchemaMapValue>())
        };
    }

    internal static DeviceStatusInboundEndpointSchemaMapValue ToModel(this DeviceStatusInboundEndpointSchemaMapValueSchema source)
    {
        return new DeviceStatusInboundEndpointSchemaMapValue
        {
            Error = source.Error?.ToModel()
        };
    }

    internal static ConfigError ToModel(this AdrBaseService.ConfigError source)
    {
        return new ConfigError
        {
            Code = source.Code,
            Message = source.Message,
            InnerError = source.InnerError,
            Details = source.Details?.Select(x => x.ToModel()).ToList()
        };
    }

    internal static DetailsSchemaElement ToModel(this DetailsSchemaElementSchema source)
    {
        return new DetailsSchemaElement
        {
            Code = source.Code,
            Message = source.Message,
            Info = source.Info,
            CorrelationId = source.CorrelationId
        };
    }

    internal static AssetStatusConfig ToModel(this AdrBaseService.AssetStatusConfigSchema source)
    {
        return new AssetStatusConfig
        {
            Error = source.Error?.ToModel(),
            LastTransitionTime = source.LastTransitionTime,
            Version = source.Version
        };
    }

    internal static AssetStatusDatasetSchemaElement ToModel(this AdrBaseService.AssetStatusDatasetSchemaElementSchema source)
    {
        return new AssetStatusDatasetSchemaElement
        {
            Error = source.Error?.ToModel(),
            Name = source.Name,
            MessageSchemaReference = source.MessageSchemaReference?.ToModel(),
        };
    }

    internal static EventsSchemaElement ToModel(this AssetStatusEventSchemaElementSchema source)
    {
        return new EventsSchemaElement
        {
            Name = source.Name,
            MessageSchemaReference = source.MessageSchemaReference?.ToModel(),
        };
    }

    internal static AssetStatusManagementGroupSchemaElement ToModel(this AssetStatusManagementGroupSchemaElementSchema source)
    {
        return new AssetStatusManagementGroupSchemaElement
        {
            Name = source.Name,
            Actions = source.Actions?.Select(x => x.ToModel()).ToList(),
        };
    }

    internal static AssetStatusManagementGroupActionSchemaElement ToModel(this AssetStatusManagementGroupActionSchemaElementSchema source)
    {
        return new AssetStatusManagementGroupActionSchemaElement
        {
            Error = source.Error?.ToModel(),
            Name = source.Name,
            RequestMessageSchemaReference = source.RequestMessageSchemaReference?.ToModel(),
            ResponseMessageSchemaReference = source.ResponseMessageSchemaReference?.ToModel()
        };
    }

    internal static AssetStatusStreamSchemaElement ToModel(this AssetStatusStreamSchemaElementSchema source)
    {
        return new AssetStatusStreamSchemaElement
        {
            Error = source.Error?.ToModel(),
            Name = source.Name,
            MessageSchemaReference = source.MessageSchemaReference?.ToModel()
        };
    }

    internal static DeviceRef ToModel(this DeviceRefSchema source)
    {
        return new DeviceRef
        {
            DeviceName = source.DeviceName,
            EndpointName = source.EndpointName
        };
    }
}
