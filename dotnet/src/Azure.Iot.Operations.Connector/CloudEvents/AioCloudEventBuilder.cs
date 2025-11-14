// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.CloudEvents;

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

/// <summary>
/// Builds AIO CloudEvents according to the message_correlation.md specification.
/// This class implements the formulas for generating CloudEvents headers from
/// ADR device, asset, and dataset models.
/// </summary>
public class AioCloudEventBuilder
{
    private readonly Device _device;
    private readonly string _endpointName;
    private readonly string _endpointAddress;
    private readonly string? _deviceName;
    private readonly string? _assetName;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="AioCloudEventBuilder"/> class.
    /// </summary>
    /// <param name="device">ADR Device model.</param>
    /// <param name="endpointName">Endpoint name from the asset's deviceRef.</param>
    /// <param name="endpointAddress">Endpoint address/protocol identifier.</param>
    /// <param name="deviceName">Device name for fallback in source/subject generation.</param>
    /// <param name="assetName">Asset name for fallback in subject generation.</param>
    public AioCloudEventBuilder(Device device, string endpointName, string endpointAddress, string? deviceName = null, string? assetName = null)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _endpointName = endpointName ?? throw new ArgumentNullException(nameof(endpointName));
        _endpointAddress = endpointAddress ?? throw new ArgumentNullException(nameof(endpointAddress));
        _deviceName = deviceName;
        _assetName = assetName;
    }
    
    /// <summary>
    /// Builds AIO CloudEvent for a specific asset and dataset combination.
    /// </summary>
    /// <param name="asset">ADR Asset model.</param>
    /// <param name="dataset">ADR AssetDataset model.</param>
    /// <param name="messageSchemaReference">Optional message schema reference reported to ADR. Used to construct the aio-sr:// DataSchema URI.</param>
    /// <param name="subSubject">Optional sub-subject to append to the CloudEvents subject.</param>
    /// <returns>Generated AIO CloudEvent.</returns>
    public AioCloudEvent Build(Asset asset, AssetDataset dataset, MessageSchemaReference? messageSchemaReference = null, string? subSubject = null)
    {
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        if (dataset == null) throw new ArgumentNullException(nameof(dataset));

        var source = BuildSource(dataset.DataSource);
        var type = BuildType("DataSet", dataset.TypeRef);
        var subject = BuildSubject(asset, dataset.Name, subSubject);
        var aioDeviceRef = BuildAioDeviceRef();
        var aioAssetRef = BuildAioAssetRef(asset);
        var dataSchema = BuildDataSchemaUri(messageSchemaReference);

        return new AioCloudEvent
        {
            Source = new Uri(source),
            Type = type,
            Subject = subject,
            DataSchema = dataSchema,
            AioDeviceRef = aioDeviceRef,
            AioAssetRef = aioAssetRef
        };
    }
    
    /// <summary>
    /// Builds AIO CloudEvent for a specific asset and event combination.
    /// </summary>
    /// <param name="asset">ADR Asset model.</param>
    /// <param name="eventGroup">ADR AssetEventGroup model.</param>
    /// <param name="assetEvent">ADR AssetEvent model.</param>
    /// <param name="messageSchemaReference">Optional message schema reference reported to ADR. Used to construct the aio-sr:// DataSchema URI.</param>
    /// <param name="subSubject">Optional sub-subject to append to the CloudEvents subject.</param>
    /// <returns>Generated AIO CloudEvent.</returns>
    public AioCloudEvent Build(Asset asset, AssetEventGroup eventGroup, AssetEvent assetEvent, MessageSchemaReference? messageSchemaReference = null, string? subSubject = null)
    {
        if (asset == null) throw new ArgumentNullException(nameof(asset));
        if (eventGroup == null) throw new ArgumentNullException(nameof(eventGroup));
        if (assetEvent == null) throw new ArgumentNullException(nameof(assetEvent));

        // Use assetEvent.DataSource, fall back to eventGroup.DataSource
        var dataSource = assetEvent.DataSource ?? eventGroup.DataSource;
        var source = BuildSource(dataSource);
        
        // Use assetEvent.TypeRef
        var type = BuildType("Event", assetEvent.TypeRef);
        
        var subject = BuildSubject(asset, eventGroup.Name, subSubject);
        
        var aioDeviceRef = BuildAioDeviceRef();
        var aioAssetRef = BuildAioAssetRef(asset);
        var dataSchema = BuildDataSchemaUri(messageSchemaReference);

        return new AioCloudEvent
        {
            Source = new Uri(source),
            Type = type,
            Subject = subject,
            DataSchema = dataSchema,
            AioDeviceRef = aioDeviceRef,
            AioAssetRef = aioAssetRef
        };
    }
    
    /// <summary>
    /// Builds the CloudEvents data_schema URI from the message schema reference reported to ADR.
    /// Formula: aio-sr://{registry_namespace}/{name}:{version}
    /// This is the schema reference that the connector reports to ADR in the status update.
    /// </summary>
    /// <param name="messageSchemaReference">The message schema reference reported to ADR, or null if no schema is defined.</param>
    /// <returns>The formatted aio-sr:// URI string, or null if no schema reference is provided.</returns>
    private string? BuildDataSchemaUri(MessageSchemaReference? messageSchemaReference)
    {
        if (messageSchemaReference == null)
        {
            return null;
        }

        return $"aio-sr://{messageSchemaReference.SchemaRegistryNamespace}/{messageSchemaReference.SchemaName}:{messageSchemaReference.SchemaVersion}";
    }
    
    /// <summary>
    /// Builds the CloudEvents source value.
    /// Formula: ms-aio:<Device-CompoundKey>|<ProtocolSpecificIdentifier>|<Device-externaldeviceId*>|<Device-Name>[/Sub-Source]
    /// Note: Device-externaldeviceId only included if different from DeviceUUID.
    /// </summary>
    /// <param name="dataSource">Optional data source sub-path to append.</param>
    /// <returns>The formatted CloudEvents source string.</returns>
    private string BuildSource(string? dataSource)
    {
        string? deviceIdentifier = null;

        // Priority 1: Device compound key (customer master data), not available untill ADR implementation
        
        // Priority 2: Protocol specific identifier (device inbound endpoint address)
        if (!string.IsNullOrWhiteSpace(_endpointAddress))
        {
            deviceIdentifier = _endpointAddress;
        }
        // Priority 3: External device ID if different from UUID
        else if (!string.IsNullOrWhiteSpace(_device.ExternalDeviceId) && !IsEqualToUuid(_device.ExternalDeviceId, _device.Uuid))
        {
            deviceIdentifier = _device.ExternalDeviceId;
        }
        // Priority 4: Device name
        else if (!string.IsNullOrWhiteSpace(_deviceName))
        {
            deviceIdentifier = _deviceName;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(deviceIdentifier))
            {
                throw new InvalidOperationException("Unable to determine device identifier: all identification fields are null or empty.");
            }
        }

        var source = $"ms-aio:{deviceIdentifier}";

        // Append sub-source if provided
        if (!string.IsNullOrWhiteSpace(dataSource))
        {
            source += $"/{dataSource}";
        }

        return source;
    }
    
    /// <summary>
    /// Builds the CloudEvents type value.
    /// Formula: ["DataSet"|"Event"]/<typeref-property-value>
    /// </summary>
    /// <param name="typePrefix">Type prefix ("DataSet" or "Event").</param>
    /// <param name="typeRef">Optional type reference from ADR model.</param>
    /// <returns>The formatted CloudEvents type string.</returns>
    private string BuildType(string typePrefix, string? typeRef)
    {
        if (string.IsNullOrWhiteSpace(typeRef))
        {
            return typePrefix;
        }

        return $"{typePrefix}/{typeRef}";
    }
    
    /// <summary>
    /// Builds the CloudEvents subject value.
    /// Formula: <Asset-CompoundKey>|<Asset-ExternalAssetId*>|<AssetName>/<DataSet-Name>|<EventGroup-Name>[/Sub-Subject]
    /// Note: Asset-externalAssetId only included if different from AssetUUID.
    /// </summary>
    /// <param name="asset">Asset identity information.</param>
    /// <param name="name">Dataset name or eventGroup.Name.</param>
    /// <param name="subSubject">Optional sub-subject to append.</param>
    /// <returns>The formatted CloudEvents subject string.</returns>
    private string BuildSubject(Asset asset, string name, string? subSubject)
    {
        string? assetIdentifier = null;

        // Priority 1: Asset compound key (customer master data), not available untill ADR implementation
        
        // Priority 2: External asset ID if different from UUID
        if (!string.IsNullOrWhiteSpace(asset.ExternalAssetId) && !IsEqualToUuid(asset.ExternalAssetId, asset.Uuid))
        {
            assetIdentifier = asset.ExternalAssetId;
        }
        // Priority 3: Asset name from Attributes
        else if(!string.IsNullOrWhiteSpace(_assetName))
        {
            assetIdentifier = _assetName;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(assetIdentifier))
            {
                throw new InvalidOperationException("Unable to determine asset identifier: all identification fields are null or empty.");
            }
        }

        var subject = $"{assetIdentifier}/{name}";

        // Append sub-subject if provided
        if (!string.IsNullOrWhiteSpace(subSubject))
        {
            subject += $"/{subSubject}";
        }

        return subject;
    }
    
    /// <summary>
    /// Builds the aiodeviceref value.
    /// Formula: ms-aio:<DeviceUUID>_<EndpointName>
    /// </summary>
    /// <returns>The formatted aiodeviceref string.</returns>
    private string BuildAioDeviceRef()
    {
        if (string.IsNullOrWhiteSpace(_device.Uuid))
        {
            throw new InvalidOperationException("Device UUID is required for aiodeviceref but is null or empty.");
        }

        return $"ms-aio:{_device.Uuid}_{_endpointName}";
    }
    
    /// <summary>
    /// Builds the aioassetref value.
    /// Formula: ms-aio:<AssetUUID>
    /// </summary>
    /// <param name="asset">Asset identity information.</param>
    /// <returns>The formatted aioassetref string.</returns>
    private string BuildAioAssetRef(Asset asset)
    {
        if (string.IsNullOrWhiteSpace(asset.Uuid))
        {
            throw new InvalidOperationException("Asset UUID is required for aioassetref but is null or empty.");
        }

        return $"ms-aio:{asset.Uuid}";
    }
    
    /// <summary>
    /// Checks if a string value equals a UUID (case-insensitive, handles various formats).
    /// Used to determine if ExternalDeviceId/ExternalAssetId should be included in CloudEvents.
    /// Per email clarification: If ExternalDeviceId/ExternalAssetId not specified by customer, 
    /// ADR applies the Device/Asset UUID to that field, so they will often be equal.
    /// </summary>
    /// <param name="value">The string value to compare.</param>
    /// <param name="uuid">The UUID to compare against.</param>
    /// <returns>True if the value equals the UUID, false otherwise.</returns>
    private bool IsEqualToUuid(string? value, string? uuid)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(uuid))
        {
            return false;
        }

        // Normalize both values by removing common UUID formatting characters
        var normalizedValue = value.Replace("-", "").Replace("{", "").Replace("}", "").Trim();
        var normalizedUuid = uuid.Replace("-", "").Replace("{", "").Replace("}", "").Trim();

        // Case-insensitive comparison
        return string.Equals(normalizedValue, normalizedUuid, StringComparison.OrdinalIgnoreCase);
    }
}
