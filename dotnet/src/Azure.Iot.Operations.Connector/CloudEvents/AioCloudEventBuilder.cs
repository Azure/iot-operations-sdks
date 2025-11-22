// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.CloudEvents;

using Services.AssetAndDeviceRegistry.Models;

/// <summary>
/// Builds AIO CloudEvents according to the message_correlation.md specification.
/// </summary>
public static class AioCloudEventBuilder
{
    /// <summary>
    /// Builds AIO CloudEvent for a specific asset and dataset combination.
    /// </summary>
    /// <param name="device">ADR Device model.</param>
    /// <param name="endpointName">Endpoint name from the asset's deviceRef.</param>
    /// <param name="endpointAddress">Endpoint address/protocol identifier.</param>
    /// <param name="asset">ADR Asset model.</param>
    /// <param name="dataset">ADR AssetDataset model.</param>
    /// <param name="messageSchemaReference">Optional message schema reference reported to ADR. Used to construct the aio-sr:// DataSchema URI.</param>
    /// <param name="deviceName">Device name for fallback in source/subject generation.</param>
    /// <param name="assetName">Asset name for fallback in subject generation.</param>
    /// <param name="subSubject">Optional sub-subject to append to the CloudEvents subject.</param>
    /// <returns>Generated AIO CloudEvent.</returns>
    public static AioCloudEvent Build(
        Device device,
        string endpointName,
        string? endpointAddress,
        Asset asset,
        AssetDataset dataset,
        MessageSchemaReference? messageSchemaReference = null,
        string? deviceName = null,
        string? assetName = null,
        string? subSubject = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(endpointName);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(dataset);

        var source = BuildSource(device, endpointAddress, deviceName, dataset.DataSource);
        var type = BuildType("DataSet", dataset.TypeRef);
        var subject = BuildSubject(asset, assetName, dataset.Name, subSubject);
        var aioDeviceRef = BuildAioDeviceRef(device, endpointName);
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
    /// <param name="device">ADR Device model.</param>
    /// <param name="endpointName">Endpoint name from the asset's deviceRef.</param>
    /// <param name="endpointAddress">Endpoint address/protocol identifier.</param>
    /// <param name="asset">ADR Asset model.</param>
    /// <param name="eventGroupName">Event group name.</param>
    /// <param name="assetEvent">ADR AssetEvent model.</param>
    /// <param name="messageSchemaReference">Optional message schema reference reported to ADR. Used to construct the aio-sr:// DataSchema URI.</param>
    /// <param name="deviceName">Device name for fallback in source/subject generation.</param>
    /// <param name="assetName">Asset name for fallback in subject generation.</param>
    /// <param name="subSubject">Optional sub-subject to append to the CloudEvents subject.</param>
    /// <returns>Generated AIO CloudEvent.</returns>
    public static AioCloudEvent Build(
        Device device,
        string endpointName,
        string? endpointAddress,
        Asset asset,
        string eventGroupName,
        AssetEvent assetEvent,
        MessageSchemaReference? messageSchemaReference = null,
        string? deviceName = null,
        string? assetName = null,
        string? subSubject = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(endpointName);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(eventGroupName);
        ArgumentNullException.ThrowIfNull(assetEvent);

        var dataSource = assetEvent.DataSource;
        var source = BuildSource(device, endpointAddress, deviceName, dataSource);

        // Use assetEvent.TypeRef
        var type = BuildType("Event", assetEvent.TypeRef);

        var subject = BuildSubject(asset, assetName, eventGroupName, subSubject);

        var aioDeviceRef = BuildAioDeviceRef(device, endpointName);
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
    /// </summary>
    /// <param name="messageSchemaReference">The message schema reference reported to ADR, or null if no schema is defined.</param>
    /// <returns>The formatted aio-sr:// URI string, or null if no schema reference is provided.</returns>
    private static string? BuildDataSchemaUri(MessageSchemaReference? messageSchemaReference)
    {
        if (messageSchemaReference == null)
        {
            return null;
        }

        return $"aio-sr://{messageSchemaReference.SchemaRegistryNamespace}/{messageSchemaReference.SchemaName}:{messageSchemaReference.SchemaVersion}";
    }

    /// <summary>
    /// Builds the CloudEvents source value.
    /// </summary>
    /// <param name="device">ADR Device model.</param>
    /// <param name="endpointAddress">Endpoint address/protocol identifier.</param>
    /// <param name="deviceName">Device name for fallback in source generation.</param>
    /// <param name="dataSource">Optional data source sub-path to append.</param>
    /// <returns>The formatted CloudEvents source string.</returns>
    private static string BuildSource(Device device, string? endpointAddress, string? deviceName, string? dataSource)
    {
        string? deviceIdentifier =
            !string.IsNullOrWhiteSpace(endpointAddress) ? endpointAddress :
            !string.IsNullOrWhiteSpace(device.ExternalDeviceId) && !IsEqualToUuid(device.ExternalDeviceId, device.Uuid) ? device.ExternalDeviceId :
            !string.IsNullOrWhiteSpace(deviceName) ? deviceName :
            throw new InvalidOperationException("Unable to determine device identifier: all identification fields are null or empty.");

        var source = $"ms-aio:{deviceIdentifier}";

        if (!string.IsNullOrWhiteSpace(dataSource))
        {
            source += dataSource.StartsWith('/') ? dataSource : $"/{dataSource}";
        }

        return source;
    }

    /// <summary>
    /// Builds the CloudEvents type value.
    /// </summary>
    /// <param name="typePrefix">Type prefix ("DataSet" or "Event").</param>
    /// <param name="typeRef">Optional type reference from ADR model.</param>
    /// <returns>The formatted CloudEvents type string.</returns>
    private static string BuildType(string typePrefix, string? typeRef)
    {
        if (string.IsNullOrWhiteSpace(typeRef))
        {
            return typePrefix;
        }

        return $"{typePrefix}/{typeRef}";
    }

    /// <summary>
    /// Builds the CloudEvents subject value.
    /// </summary>
    /// <param name="asset">ADR Asset model.</param>
    /// <param name="assetName">Asset name for fallback in subject generation.</param>
    /// <param name="name">Dataset name or event group name.</param>
    /// <param name="subSubject">Optional sub-subject to append.</param>
    /// <returns>The formatted CloudEvents subject string.</returns>
    private static string BuildSubject(Asset asset, string? assetName, string name, string? subSubject)
    {
        string? assetIdentifier =
            !string.IsNullOrWhiteSpace(asset.ExternalAssetId) && !IsEqualToUuid(asset.ExternalAssetId, asset.Uuid) ? asset.ExternalAssetId :
            !string.IsNullOrWhiteSpace(assetName) ? assetName :
            throw new InvalidOperationException("Unable to determine asset identifier: all identification fields are null or empty.");

        var subject = $"{assetIdentifier}/{name}";

        if (!string.IsNullOrWhiteSpace(subSubject))
        {
            subject += $"/{subSubject}";
        }

        return subject;
    }

    /// <summary>
    /// Builds the aiodeviceref value.
    /// </summary>
    /// <param name="device">ADR Device model.</param>
    /// <param name="endpointName">Endpoint name from the asset's deviceRef.</param>
    /// <returns>The formatted aiodeviceref string.</returns>
    private static string BuildAioDeviceRef(Device device, string endpointName)
    {
        if (string.IsNullOrWhiteSpace(device.Uuid))
        {
            throw new InvalidOperationException("Device UUID is required for aiodeviceref but is null or empty.");
        }

        return $"ms-aio:{device.Uuid}_{endpointName}";
    }

    /// <summary>
    /// Builds the aioassetref value.
    /// </summary>
    /// <param name="asset">ADR Asset model.</param>
    /// <returns>The formatted aioassetref string.</returns>
    private static string BuildAioAssetRef(Asset asset)
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
    private static bool IsEqualToUuid(string? value, string? uuid)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(uuid))
        {
            return false;
        }

        var normalizedValue = value.Replace("-", "").Replace("{", "").Replace("}", "").Trim();
        var normalizedUuid = uuid.Replace("-", "").Replace("{", "").Replace("}", "").Trim();

        return string.Equals(normalizedValue, normalizedUuid, StringComparison.OrdinalIgnoreCase);
    }
}
