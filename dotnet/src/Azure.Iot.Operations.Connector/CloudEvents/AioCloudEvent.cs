// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Telemetry;

namespace Azure.Iot.Operations.Connector.CloudEvents;

/// <summary>
/// Azure IoT Operations CloudEvent with automatic metadata population.
/// Contains all CloudEvents fields with AIO-specific extensions (aiodeviceref, aioassetref).
/// This class holds all the CloudEvents headers that will be automatically
/// populated based on connector, device, asset, and dataset configuration.
/// </summary>
public class AioCloudEvent
{
    /// <summary>
    /// Generated CloudEvents source URI.
    /// Formula: ms-aio:<Device-CompoundKey>|<ProtocolSpecificIdentifier>|<Device-externaldeviceId*>|<Device-Name>[/Sub-Source]
    /// </summary>
    public required Uri Source { get; init; }

    /// <summary>
    /// Generated CloudEvents type string.
    /// Formula: ["DataSet"|"Event"]/<typeref-property-value>
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Generated CloudEvents subject string.
    /// Formula: <Asset-CompoundKey>|<Asset-ExternalAssetId*>|<AssetName>/<DataSet-Name>|<EventGroup-Name>[/Sub-Subject]
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Schema registry ID for the payload schema.
    /// Maps to CloudEvents dataschema attribute.
    /// </summary>
    public string? DataSchema { get; init; }

    /// <summary>
    /// Generated aiodeviceref value.
    /// Formula: ms-aio:<DeviceUUID>_<EndpointName>
    /// AIO-specific extension attribute.
    /// </summary>
    public required string AioDeviceRef { get; init; }

    /// <summary>
    /// Generated aioassetref value.
    /// Formula: ms-aio:<AssetUUID>
    /// AIO-specific extension attribute.
    /// </summary>
    public required string AioAssetRef { get; init; }

    /// <summary>
    /// Creates a standard CloudEvent from this AIO CloudEvent metadata.
    /// </summary>
    /// <param name="time">Timestamp of when the occurrence happened. Defaults to current UTC time.</param>
    /// <param name="id">Event identifier. Defaults to a new GUID.</param>
    /// <returns>A CloudEvent instance with all standard fields populated.</returns>
    public CloudEvent ToCloudEvent(DateTime? time = null, string? id = null)
    {
        return new CloudEvent(Source, Type)
        {
            Subject = Subject,
            DataSchema = DataSchema,
            Time = time ?? DateTime.UtcNow,
            Id = id ?? Guid.NewGuid().ToString()
        };
    }

    /// <summary>
    /// Gets the AIO-specific extension attributes as a dictionary.
    /// Use this to populate CloudEvents extension attributes in OutgoingTelemetryMetadata.UserData.
    /// </summary>
    /// <returns>Dictionary containing aiodeviceref and aioassetref extension attributes.</returns>
    public Dictionary<string, string> GetExtensions()
    {
        return new Dictionary<string, string>
        {
            ["aiodeviceref"] = AioDeviceRef,
            ["aioassetref"] = AioAssetRef
        };
    }
}
