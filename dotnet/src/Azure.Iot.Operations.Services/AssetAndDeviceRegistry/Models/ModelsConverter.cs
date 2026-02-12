// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Generated;
using Azure.Iot.Operations.Services.StateStore;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

internal static class ModelsConverter
{
    internal static CreateOrUpdateDiscoveredAssetResponsePayload ToModel(this Generated.CreateOrUpdateDiscoveredAssetResponsePayload source)
    {
        return new()
        {
            DiscoveredAssetResponse = source.DiscoveredAssetResponse.ToModel()
        };
    }

    internal static DiscoveredAssetResponseSchema ToModel(this Generated.DiscoveredAssetResponseSchema source)
    {
        return new()
        {
            DiscoveryId = source.DiscoveryId,
            Version = source.Version,
        };
    }

    internal static DiscoveredDeviceResponseSchema ToModel(this Generated.DiscoveredDeviceResponseSchema source)
    {
        return new()
        {
            DiscoveryId = source.DiscoveryId,
            Version = source.Version,
        };
    }

    internal static CreateOrUpdateDiscoveredDeviceResponsePayload ToModel(this Generated.CreateOrUpdateDiscoveredDeviceResponsePayload source)
    {
        return new()
        {
            DiscoveredDeviceResponse = source.DiscoveredDeviceResponse.ToModel(),
        };
    }

    internal static SetNotificationPreferenceForAssetUpdatesResponsePayload ToModel(this Generated.SetNotificationPreferenceForAssetUpdatesResponsePayload source)
    {
        return new()
        {
            ResponsePayload = source.ResponsePayload
        };
    }

    internal static SetNotificationPreferenceForDeviceUpdatesResponsePayload ToModel(this Generated.SetNotificationPreferenceForDeviceUpdatesResponsePayload source)
    {
        return new()
        {
            ResponsePayload = source.ResponsePayload
        };
    }

    internal static ConfigStatus ToModel(this Generated.ConfigStatus source)
    {
        return new ConfigStatus
        {
            Error = source.Error?.ToModel(),
            Version = source.Version,
            LastTransitionTime = source.LastTransitionTime
        };
    }

    internal static ConfigError ToModel(this Generated.ConfigError source)
    {
        return new ConfigError
        {
            Code = source.Code,
            Details = source.Details?.Select(x => x.ToModel()).ToList(),
            Message = source.Message,
        };
    }

    internal static AssetStatus ToModel(this Generated.AssetStatus source)
    {
        return new AssetStatus
        {
            Config = source.Config?.ToModel(),
            Datasets = source.Datasets?.Select(x => x.ToModel()).ToList(),
            EventGroups = source.EventGroups?.Select(x => x.ToModel()).ToList(),
            ManagementGroups = source.ManagementGroups?.Select(x => x.ToModel()).ToList(),
            Streams = source.Streams?.Select(x => x.ToModel()).ToList()
        };
    }

    internal static Asset ToModel(this Generated.Asset source)
    {
        return new Asset
        {
            AssetTypeRefs = source.AssetTypeRefs,
            Attributes = source.Attributes,
            Datasets = source.Datasets?.Select(x => x.ToModel()).ToList(),
            DefaultDatasetsConfiguration = source.DefaultDatasetsConfiguration,
            DefaultDatasetsDestinations = source.DefaultDatasetsDestinations?.Select(x => x.ToModel()).ToList(),
            DefaultEventsConfiguration = source.DefaultEventsConfiguration,
            DefaultEventsDestinations = source.DefaultEventsDestinations?.Select(x => x.ToModel()).ToList(),
            DefaultManagementGroupsConfiguration = source.DefaultManagementGroupsConfiguration,
            DefaultStreamsConfiguration = source.DefaultStreamsConfiguration,
            DefaultStreamsDestinations = source.DefaultStreamsDestinations?.Select(x => x.ToModel()).ToList(),
            Description = source.Description,
            DeviceRef = source.DeviceRef.ToModel(),
            DiscoveredAssetRefs = source.DiscoveredAssetRefs,
            DisplayName = source.DisplayName,
            DocumentationUri = source.DocumentationUri,
            Enabled = source.Enabled,
            EventGroups = source.EventGroups?.Select(x => x.ToModel()).ToList(),
            ExternalAssetId = source.ExternalAssetId,
            HardwareRevision = source.HardwareRevision,
            LastTransitionTime = source.LastTransitionTime,
            ManagementGroups = source.ManagementGroups?.Select(x => x.ToModel()).ToList(),
            Manufacturer = source.Manufacturer,
            ManufacturerUri = source.ManufacturerUri,
            Model = source.Model,
            ProductCode = source.ProductCode,
            SerialNumber = source.SerialNumber,
            SoftwareRevision = source.SoftwareRevision,
            Streams = source.Streams?.Select(x => x.ToModel()).ToList(),
            Uuid = source.Uuid,
            Version = source.Version,
        };
    }

    internal static Models.AssetEventGroup ToModel(this Generated.AssetEventGroupSchemaElementSchema source)
    {
        return new AssetEventGroup()
        {
            DataSource = source.DataSource,
            DefaultEventsDestinations = source.DefaultEventsDestinations?.Select(x => x.ToModel()).ToList(),
            EventGroupConfiguration = source.EventGroupConfiguration,
            Events = source.Events?.Select(x => x.ToModel()).ToList(),
            Name = source.Name,
            TypeRef = source.TypeRef,
        };
    }

    internal static Models.AssetEventGroupStatus ToModel(this Generated.AssetEventGroupStatusSchemaElementSchema source)
    {
        return new AssetEventGroupStatus()
        {
            Events = source.Events?.Select(x => x.ToModel()).ToList(),
            Name = source.Name,
        };
    }

    internal static Models.AkriServiceError ToModel(this Generated.AkriServiceError source)
    {
        return new Models.AkriServiceError
        {
            Code = (Code)(int)source.Code,
            Message = source.Message,
            Timestamp = source.Timestamp,
        };
    }

    internal static Models.AkriServiceError ToModel(this Generated.AkriServiceError source)
    {
        return new Models.AkriServiceError
        {
            Code = (Code)(int)source.Code,
            Message = source.Message,
            Timestamp = source.Timestamp,
        };
    }

    public static Device ToModel(this Generated.Device source)
    {
        return new Device
        {
            Attributes = source.Attributes,
            DiscoveredDeviceRef = source.DiscoveredDeviceRef,
            Enabled = source.Enabled,
            Endpoints = source.Endpoints?.ToModel(),
            ExternalDeviceId = source.ExternalDeviceId,
            LastTransitionTime = source.LastTransitionTime,
            Manufacturer = source.Manufacturer,
            Model = source.Model,
            OperatingSystem = source.OperatingSystem,
            OperatingSystemVersion = source.OperatingSystemVersion,
            Uuid = source.Uuid,
            Version = source.Version
        };
    }

    public static NotificationResponse ToModel(this SetNotificationPreferenceForDeviceUpdatesResponsePayload source)
    {
        return new NotificationResponse
        {
            ResponsePayload = source.ResponsePayload
        };
    }

    public static NotificationResponse ToModel(this SetNotificationPreferenceForAssetUpdatesResponsePayload source)
    {
        return new NotificationResponse
        {
            ResponsePayload = source.ResponsePayload
        };
    }

    internal static MessageSchemaReference ToModel(this Generated.MessageSchemaReference source)
    {
        return new MessageSchemaReference
        {
            SchemaName = source.SchemaName,
            SchemaRegistryNamespace = source.SchemaRegistryNamespace,
            SchemaVersion = source.SchemaVersion
        };
    }

    internal static AssetStream ToModel(this AssetStreamSchemaElementSchema source)
    {
        return new AssetStream
        {
            Name = source.Name,
            Destinations = source.Destinations?.Select(x => x.ToModel()).ToList(),
            StreamConfiguration = source.StreamConfiguration,
            TypeRef = source.TypeRef
        };
    }

    internal static AssetManagementGroup ToModel(this AssetManagementGroupSchemaElementSchema source)
    {
        return new AssetManagementGroup
        {
            Name = source.Name,
            Actions = source.Actions?.Select(x => x.ToModel()).ToList(),
            DefaultTimeoutInSeconds = source.DefaultTimeoutInSeconds,
            DataSource = source.DataSource,
            DefaultTopic = source.DefaultTopic,
            ManagementGroupConfiguration = source.ManagementGroupConfiguration,
            TypeRef = source.TypeRef
        };
    }

    internal static AssetManagementGroupAction ToModel(this AssetManagementGroupActionSchemaElementSchema source)
    {
        return new AssetManagementGroupAction
        {
            Name = source.Name,
            TargetUri = source.TargetUri,
            TimeoutInSeconds = source.TimeoutInSeconds,
            Topic = source.Topic,
            TypeRef = source.TypeRef,
            ActionConfiguration = source.ActionConfiguration,
            ActionType = source.ActionType.ToModel()
        };
    }

    internal static DatasetDestination ToModel(this Generated.DatasetDestination source)
    {
        return new DatasetDestination
        {
            Target = source.Target.ToModel(),
            Configuration = source.Configuration.ToModel()
        };
    }

    internal static AssetManagementGroupActionType ToModel(this Generated.AssetManagementGroupActionType source)
    {
        return (AssetManagementGroupActionType)(int)source;
    }

    internal static DatasetTarget ToModel(this Generated.DatasetTarget source)
    {
        return (DatasetTarget)(int)source;
    }

    internal static DestinationConfiguration ToModel(this Generated.DestinationConfiguration source)
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

    internal static Retain ToModel(this Generated.Retain source)
    {
        return (Retain)(int)source;
    }

    internal static QoS ToModel(this Generated.Qos source)
    {
        return (QoS)(int)source;
    }

    internal static AssetDataset ToModel(this AssetDatasetSchemaElementSchema source)
    {
        return new AssetDataset
        {
            Name = source.Name,
            DataPoints = source.DataPoints?.Select(x => x.ToModel()).ToList(),
            DataSource = source.DataSource,
            TypeRef = source.TypeRef,
            Destinations = source.Destinations?.Select(x => x.ToModel()).ToList(),
            DatasetConfiguration = source.DatasetConfiguration
        };
    }

    internal static AssetDatasetDataPoint ToModel(this AssetDatasetDataPointSchemaElementSchema source)
    {
        return new AssetDatasetDataPoint
        {
            Name = source.Name,
            DataSource = source.DataSource,
            DataPointConfiguration = source.DataPointConfiguration,
            TypeRef = source.TypeRef,
        };
    }

    internal static AssetEvent ToModel(this AssetEventSchemaElementSchema source)
    {
        return new AssetEvent
        {
            Name = source.Name,
            Destinations = source.Destinations?.Select(x => x.ToModel()).ToList(),
            EventConfiguration = source.EventConfiguration,
            DataSource = source.DataSource,
            TypeRef = source.TypeRef
        };
    }

    internal static EventStreamDestination ToModel(this Generated.EventStreamDestination source)
    {
        return new EventStreamDestination
        {
            Configuration = source.Configuration.ToModel(),
            Target = source.Target.ToModel(),
        };
    }

    internal static EventStreamTarget ToModel(this Generated.EventStreamTarget source)
    {
        return (EventStreamTarget)(int)source;
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
            CertificateSecretName = source.CertificateSecretName,
            IntermediateCertificatesSecretName = source.IntermediateCertificatesSecretName,
            KeySecretName = source.KeySecretName,
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

    internal static DeviceEndpoints ToModel(this DeviceEndpointsSchema source)
    {
        return new DeviceEndpoints
        {
            Inbound = new Dictionary<string, InboundEndpointSchemaMapValue>(
                source.Inbound?.Select(x => new KeyValuePair<string, InboundEndpointSchemaMapValue>(x.Key, x.Value.ToModel())) ??
                new Dictionary<string, InboundEndpointSchemaMapValue>()),
            Outbound = source.Outbound?.ToModel()
        };
    }

    internal static InboundEndpointSchemaMapValue ToModel(this InboundSchemaMapValueSchema source)
    {
        return new InboundEndpointSchemaMapValue
        {
            Address = source.Address,
            AdditionalConfiguration = source.AdditionalConfiguration,
            Version = source.Version,
            EndpointType = source.EndpointType,
            Authentication = source.Authentication?.ToModel(),
            TrustSettings = source.TrustSettings?.ToModel()
        };
    }

    internal static TrustSettings ToModel(this TrustSettingsSchema source)
    {
        return new TrustSettings
        {
            TrustList = source.TrustList
        };
    }

    internal static DeviceStatus ToModel(this Generated.DeviceStatus source)
    {
        return new DeviceStatus
        {
            Endpoints = source.Endpoints?.ToModel(),
            Config = source.Config?.ToModel()
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

    internal static ConfigErrorDetails ToModel(this DetailsSchemaElementSchema source)
    {
        return new ConfigErrorDetails
        {
            Code = source.Code,
            Message = source.Message,
            Info = source.Info,
            CorrelationId = source.CorrelationId
        };
    }

    internal static AssetDatasetEventStreamStatus ToModel(this Generated.AssetDatasetEventStreamStatus source)
    {
        return new AssetDatasetEventStreamStatus
        {
            Name = source.Name,
            MessageSchemaReference = source.MessageSchemaReference?.ToModel(),
            Error = source.Error?.ToModel()
        };
    }

    internal static AssetManagementGroupStatus ToModel(this AssetManagementGroupStatusSchemaElementSchema source)
    {
        return new AssetManagementGroupStatus
        {
            Name = source.Name,
            Actions = source.Actions?.Select(x => x.ToModel()).ToList(),
        };
    }

    internal static AssetManagementGroupActionStatus ToModel(this AssetManagementGroupActionStatusSchemaElementSchema source)
    {
        return new AssetManagementGroupActionStatus
        {
            Error = source.Error?.ToModel(),
            Name = source.Name,
            RequestMessageSchemaReference = source.RequestMessageSchemaReference?.ToModel(),
            ResponseMessageSchemaReference = source.ResponseMessageSchemaReference?.ToModel()
        };
    }

    internal static AssetDeviceRef ToModel(this Generated.AssetDeviceRef source)
    {
        return new AssetDeviceRef
        {
            DeviceName = source.DeviceName,
            EndpointName = source.EndpointName
        };
    }

    internal static DeviceOutboundEndpoint ToModel(this Generated.DeviceOutboundEndpoint source)
    {
        return new DeviceOutboundEndpoint
        {
            Address = source.Address,
            EndpointType = source.EndpointType
        };
    }

    internal static OutboundSchema ToModel(this Generated.OutboundSchema source)
    {
        return new OutboundSchema
        {
            Assigned = new Dictionary<string, DeviceOutboundEndpoint>(source.Assigned.Select(x => new KeyValuePair<string, DeviceOutboundEndpoint>(x.Key, x.Value.ToModel()))),
            Unassigned = new Dictionary<string, DeviceOutboundEndpoint>(source.Unassigned?.Select(x => new KeyValuePair<string, DeviceOutboundEndpoint>(x.Key, x.Value.ToModel())) ??
                                                                        new Dictionary<string, DeviceOutboundEndpoint>())
        };
    }
}
