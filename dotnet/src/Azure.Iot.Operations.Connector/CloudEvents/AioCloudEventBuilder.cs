// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Connector.CloudEvents;

using Azure.Iot.Operations.Protocol;
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
    /// <param name="deviceName">Device name for fallback in source/subject generation.</param>
    /// <param name="endpointName">Endpoint name from the asset's deviceRef.</param>
    /// <param name="asset">ADR Asset model.</param>
    /// <param name="dataset">ADR AssetDataset model.</param>
    /// <param name="assetName">Asset name.</param>
    /// <param name="protocolSpecificIdentifier">Optional protocol specific identifier.</param>
    /// <param name="messageSchemaReference">Optional message schema reference reported to ADR. Used to construct the aio-sr:// DataSchema URI.</param>
    /// <returns>Generated AIO CloudEvent.</returns>
    public static AioCloudEvent Build(
        Device device,
        string deviceName,
        string endpointName,
        Asset asset,
        AssetDataset dataset,
        string assetName,
        string? protocolSpecificIdentifier = null,
        MessageSchemaReference? messageSchemaReference = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrEmpty(deviceName);
        ArgumentException.ThrowIfNullOrEmpty(endpointName);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentException.ThrowIfNullOrEmpty(assetName);

        var source = BuildSource(device, protocolSpecificIdentifier, deviceName, dataset.DataSource);
        var type = BuildType("DataSet", dataset.TypeRef);
        var subject = BuildSubject(asset, assetName, dataset.Name);
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
    /// <param name="deviceName">Device name for fallback in source/subject generation.</param>
    /// <param name="endpointName">Endpoint name from the asset's deviceRef.</param>
    /// <param name="asset">ADR Asset model.</param>
    /// <param name="assetEvent">ADR AssetEvent model.</param>
    /// <param name="assetName">Asset name for fallback in subject generation.</param>
    /// <param name="eventGroupName">Event group name.</param>
    /// <param name="protocolSpecificIdentifier">Optional protocol specific identifier.</param>
    /// <param name="messageSchemaReference">Optional message schema reference reported to ADR. Used to construct the aio-sr:// DataSchema URI.</param>
    /// <returns>Generated AIO CloudEvent.</returns>
    public static AioCloudEvent Build(
        Device device,
        string deviceName,
        string endpointName,
        Asset asset,
        AssetEvent assetEvent,
        string assetName,
        string eventGroupName,
        string? protocolSpecificIdentifier = null,
        MessageSchemaReference? messageSchemaReference = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrEmpty(deviceName);
        ArgumentException.ThrowIfNullOrEmpty(endpointName);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrEmpty(assetName);
        ArgumentException.ThrowIfNullOrEmpty(eventGroupName);
        ArgumentNullException.ThrowIfNull(assetEvent);

        var dataSource = assetEvent.DataSource;
        var source = BuildSource(device, protocolSpecificIdentifier, deviceName, dataSource);

        // Use assetEvent.TypeRef
        var type = BuildType("Event", assetEvent.TypeRef);

        var subject = BuildSubject(asset, assetName, eventGroupName, assetEvent.Name);

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
    /// Resolves the Cloud Event and AIO extension headers for a dataset, using the values in
    /// <paramref name="cloudEvent"/> and deriving the <c>Automatic</c> ones.
    /// </summary>
    internal static (CloudEvent CloudEvent, Dictionary<string, string> Extensions) Resolve(
        DataOperationCloudEvent cloudEvent,
        Device device,
        string deviceName,
        string endpointName,
        Asset asset,
        AssetDataset dataset,
        string assetName,
        MessageSchemaReference? messageSchemaReference,
        DateTime automaticTime)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        return Resolve(cloudEvent, device, deviceName, endpointName, asset, assetName, "DataSet", dataset.TypeRef, dataset.DataSource, dataset.Name, null, messageSchemaReference, automaticTime);
    }

    /// <summary>
    /// Resolves the Cloud Event and AIO extension headers for an event, using the values in
    /// <paramref name="cloudEvent"/> and deriving the <c>Automatic</c> ones.
    /// </summary>
    internal static (CloudEvent CloudEvent, Dictionary<string, string> Extensions) Resolve(
        DataOperationCloudEvent cloudEvent,
        Device device,
        string deviceName,
        string endpointName,
        Asset asset,
        AssetEvent assetEvent,
        string assetName,
        string eventGroupName,
        MessageSchemaReference? messageSchemaReference,
        DateTime automaticTime)
    {
        ArgumentNullException.ThrowIfNull(assetEvent);
        ArgumentException.ThrowIfNullOrEmpty(eventGroupName);
        return Resolve(cloudEvent, device, deviceName, endpointName, asset, assetName, "Event", assetEvent.TypeRef, assetEvent.DataSource, eventGroupName, assetEvent.Name, messageSchemaReference, automaticTime);
    }

    private static (CloudEvent CloudEvent, Dictionary<string, string> Extensions) Resolve(
        DataOperationCloudEvent cloudEvent,
        Device device,
        string deviceName,
        string endpointName,
        Asset asset,
        string assetName,
        string typePrefix,
        string? typeRef,
        string? dataSource,
        string name,
        string? subSubject,
        MessageSchemaReference? messageSchemaReference,
        DateTime automaticTime)
    {
        ArgumentNullException.ThrowIfNull(cloudEvent);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrEmpty(deviceName);
        ArgumentException.ThrowIfNullOrEmpty(endpointName);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrEmpty(assetName);

        Uri source = cloudEvent.Source.Kind switch
        {
            CloudEventValueKind.Custom => cloudEvent.Source.CustomSource!,
            CloudEventValueKind.AutomaticWithProtocolSpecificIdentifier => new Uri(BuildSource(device, cloudEvent.Source.ProtocolSpecificIdentifier, deviceName, dataSource)),
            _ => new Uri(BuildSource(device, null, deviceName, dataSource)),
        };

        string type = cloudEvent.Type.Kind == CloudEventValueKind.Custom
            ? cloudEvent.Type.Value!
            : BuildType(typePrefix, typeRef);

        var result = new CloudEvent(source, type)
        {
            DataSchema = cloudEvent.DataSchema.Kind switch
            {
                CloudEventValueKind.Custom => cloudEvent.DataSchema.Value,
                CloudEventValueKind.None => null,
                _ => BuildDataSchemaUri(messageSchemaReference),
            },
            Time = cloudEvent.Time.Kind switch
            {
                CloudEventValueKind.Custom => cloudEvent.Time.Value,
                CloudEventValueKind.None => null,
                _ => automaticTime,
            },
        };

        if (cloudEvent.Id.Kind == CloudEventValueKind.Custom)
        {
            result.Id = cloudEvent.Id.Value;
        }

        switch (cloudEvent.Subject.Kind)
        {
            case CloudEventValueKind.Custom:
                result.Subject = cloudEvent.Subject.Value;
                break;
            case CloudEventValueKind.None:
                result.Subject = null;
                break;
            case CloudEventValueKind.PublishTopic:
                // Leaving the subject unset makes the telemetry sender use the publish topic
                break;
            default:
                result.Subject = BuildSubject(asset, assetName, name, subSubject);
                break;
        }

        var extensions = new Dictionary<string, string>();
        switch (cloudEvent.AioDeviceRef.Kind)
        {
            case CloudEventValueKind.Custom:
                extensions["aiodeviceref"] = cloudEvent.AioDeviceRef.Value!;
                break;
            case CloudEventValueKind.None:
                break;
            default:
                extensions["aiodeviceref"] = BuildAioDeviceRef(device, endpointName);
                break;
        }

        switch (cloudEvent.AioAssetRef.Kind)
        {
            case CloudEventValueKind.Custom:
                extensions["aioassetref"] = cloudEvent.AioAssetRef.Value!;
                break;
            case CloudEventValueKind.None:
                break;
            default:
                extensions["aioassetref"] = BuildAioAssetRef(asset);
                break;
        }

        return (result, extensions);
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
    /// <param name="protocolSpecificIdentifier">Protocol specific identifier.</param>
    /// <param name="deviceName">Device name for fallback in source generation.</param>
    /// <param name="dataSource">Optional data source sub-path to append.</param>
    /// <returns>The formatted CloudEvents source string.</returns>
    private static string BuildSource(Device device, string? protocolSpecificIdentifier, string deviceName, string? dataSource)
    {
        string? deviceIdentifier =
            !string.IsNullOrWhiteSpace(protocolSpecificIdentifier) ? protocolSpecificIdentifier :
            !string.IsNullOrWhiteSpace(device.ExternalDeviceId) && !IsEqualToUuid(device.ExternalDeviceId, device.Uuid) ? device.ExternalDeviceId :
            deviceName;

        var source = $"ms-aio:{deviceIdentifier}";

        if (!string.IsNullOrWhiteSpace(dataSource) && IsValidUriParts($"{source}/", dataSource.TrimStart('/')))
        {
            source += $"/{dataSource.TrimStart('/')}";
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
    private static string BuildSubject(Asset asset, string assetName, string name, string? subSubject = null)
    {
        string? assetIdentifier =
            !string.IsNullOrWhiteSpace(asset.ExternalAssetId) && !IsEqualToUuid(asset.ExternalAssetId, asset.Uuid) ? asset.ExternalAssetId :
            !string.IsNullOrWhiteSpace(assetName) ? assetName :
            throw new InvalidOperationException("Unable to determine asset identifier: all identification fields are null or empty.");

        var subject = $"{assetIdentifier}/{name}";

        if (!string.IsNullOrWhiteSpace(subSubject) && IsValidUriParts(null, subSubject.TrimStart('/')))
        {
            subject += $"/{subSubject.TrimStart('/')}";
        }

        return subject;
    }

    private static bool IsValidUriParts(string? baseUri, string? segment)
    {
        return !string.IsNullOrEmpty(segment) && Uri.TryCreate(new Uri(baseUri ?? "http://dummy/"), segment, out _);
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
